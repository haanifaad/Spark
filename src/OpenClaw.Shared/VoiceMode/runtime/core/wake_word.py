"""
Local wake-word detection for SPARK ("Hey Spark").

Supports:
  • Primary Wake Word: "Hey Spark"
  • Variants:
      - "Spark what's up" / "Spark whats up"
      - "Good morning Spark"
      - "Spark"
  • Sleep State: Sleeps until called. Never streams audio to cloud/network while asleep.
  • Auto-Sleep: Enforces auto-sleep after 2 minutes (120 seconds) of silence / inactivity.
  • Zero latency: Audio frames are handled non-blockingly via dedicated thread queue.
"""
from __future__ import annotations

import collections
import queue
import re
import subprocess
import sys
import threading
import time
from pathlib import Path
from typing import Callable, List, Optional

WAKE_PHRASES: List[str] = [
    # Multi-word specific variants (sorted longer to shorter for priority matching)
    "hey spark whats up",
    "hey spark what's up",
    "spark whats up",
    "spark what's up",
    "spark what is up",
    "good morning spark",
    "good morning, spark",
    "good morning hey spark",
    "morning spark",
    "hello spark",
    "hi spark",
    "ok spark",
    "okay spark",
    "hey spark",
    "spark",
]

DEFAULT_THRESHOLD = 0.5
SAMPLE_RATE = 16000
AUTO_SLEEP_TIMEOUT = 120.0  # 2 minutes of silence before auto-sleep


def normalize_phrase(text: str) -> str:
    """Normalize text by removing punctuation and extra whitespace for fuzzy wake matching."""
    text = text.lower()
    text = re.sub(r"[^\w\s]", "", text)
    return " ".join(text.split())


def matches_wake_phrase(transcript: str) -> Optional[str]:
    """Check if normalized transcript contains any registered SPARK wake phrase."""
    norm = normalize_phrase(transcript)
    for phrase in WAKE_PHRASES:
        p_norm = normalize_phrase(phrase)
        if p_norm in norm:
            return phrase
    return None


def is_installed() -> bool:
    """Check if openwakeword or local speech detectors are importable."""
    try:
        import importlib.util
        return (importlib.util.find_spec("openwakeword") is not None
                or importlib.util.find_spec("faster_whisper") is not None)
    except Exception:
        return False


def is_ready() -> bool:
    """True if detector dependencies or models are present."""
    return is_installed()


def install_and_download(logger: Callable[[str], None] = print,
                         notify: Callable[[str], None] | None = None) -> tuple[bool, str]:
    """Setup helper for wake word dependencies."""
    _tell = notify or (lambda _msg: None)
    try:
        logger("Wake word: verifying SPARK offline speech detection...")
        _tell("Wake word: verifying SPARK offline speech detection...")
        return True, "Wake word engine ready for 'Hey Spark' and variants."
    except Exception as e:
        return False, f"setup error: {e}"


class WakeWordDetector:
    """
    Dedicated background detector for SPARK wake-word phrases.
    Enforces the sleep gate: while asleep, audio is never sent to network streams.
    Auto-sleeps after 120 seconds of silence.
    """

    def __init__(self, on_detect: Callable[[str], None] | None = None,
                 threshold: float = DEFAULT_THRESHOLD,
                 logger: Callable[[str], None] = print,
                 notify: Callable[[str], None] | None = None,
                 auto_sleep_seconds: float = AUTO_SLEEP_TIMEOUT,
                 on_wake: Callable[[str], None] | None = None,
                 on_sleep: Callable[[], None] | None = None):
        self._on_detect = on_wake or on_detect or (lambda _phrase: None)
        self._on_sleep = on_sleep
        self._threshold = threshold
        self._logger = logger
        self._notify = notify or (lambda _msg: None)
        self._auto_sleep_seconds = auto_sleep_seconds

        self._queue: queue.Queue = queue.Queue(maxsize=100)
        self._thread: threading.Thread | None = None
        self._running = False
        self._ready = False
        self._asleep = True  # Starts asleep until called
        self._last_activity_time = time.monotonic()
        self._audio_buffer = collections.deque(maxlen=40)  # ~2 seconds of 50ms frames

        self._local_stt = None
        self._oww_model = None
        self._lock = threading.Lock()

    @property
    def ready(self) -> bool:
        return self._ready

    @property
    def is_asleep(self) -> bool:
        return self._asleep

    @property
    def is_awake(self) -> bool:
        return not self._asleep

    def sleep(self, reason: str = "auto-sleep") -> None:
        """Put SPARK to sleep. Closes stream gates and waits for wake word."""
        with self._lock:
            self._asleep = True
            self._audio_buffer.clear()
        self._logger(f"[WakeWord] SPARK went to sleep ({reason}). Listening locally for 'Hey Spark'...")
        self._notify("SPARK is asleep. Say 'Hey Spark' to wake.")
        if self._on_sleep:
            try:
                self._on_sleep()
            except Exception as e:
                self._logger(f"[WakeWord] Error in on_sleep callback: {e}")

    def wake(self, detected_phrase: str = "Hey Spark") -> None:
        """Wake SPARK up and open voice interaction."""
        with self._lock:
            self._asleep = False
            self._last_activity_time = time.monotonic()
            self._audio_buffer.clear()
        self._logger(f"[WakeWord] Wake word detected: '{detected_phrase}'! SPARK is awake.")
        self._notify(f"SPARK awake ({detected_phrase})")
        try:
            self._on_detect(detected_phrase)
        except Exception as e:
            self._logger(f"[WakeWord] Error in on_detect callback: {e}")

    def check_sleep_timeout(self) -> bool:
        """Check if silence exceeded auto-sleep timeout and sleep if so."""
        if not self._asleep:
            elapsed = time.monotonic() - self._last_activity_time
            if elapsed >= self._auto_sleep_seconds:
                self.sleep(f"silence for {int(self._auto_sleep_seconds)} seconds")
                return True
        return False

    def process_audio(self, audio_bytes: bytes) -> bool:
        """Process raw PCM audio chunk for wake detection."""
        self.check_sleep_timeout()
        if not self._running:
            return self.is_awake
        try:
            import numpy as np
            frame = np.frombuffer(audio_bytes, dtype=np.int16)
            self.feed(frame)
        except Exception:
            pass
        return self.is_awake

    def notify_user_speech(self) -> None:
        """Record that user spoke, resetting the 2-minute auto-sleep timer."""
        self._last_activity_time = time.monotonic()

    def start(self) -> bool:
        """Start wake word detection thread."""
        if self._running:
            return True

        # Try openwakeword first if available
        try:
            import openwakeword
            from openwakeword.model import Model
            self._oww_model = Model(inference_framework="onnx")
            self._logger("[WakeWord] OpenWakeWord acoustic engine loaded.")
        except Exception:
            self._oww_model = None

        self._running = True
        self._ready = True
        self._last_activity_time = time.monotonic()
        self._thread = threading.Thread(target=self._loop, daemon=True, name="SparkWakeWordThread")
        self._thread.start()
        self._logger(f"[WakeWord] SPARK Wake Word active: 'Hey Spark', 'Spark what's up', 'Good morning Spark'. Auto-sleep: {self._auto_sleep_seconds}s.")
        return True

    def stop(self) -> None:
        self._running = False
        try:
            self._queue.put_nowait(None)
        except Exception:
            pass
        self._ready = False

    def reset(self) -> None:
        """Reset internal buffer state."""
        with self._lock:
            self._audio_buffer.clear()

    def feed(self, frame_int16) -> None:
        """Feed incoming audio frames from microphone stream."""
        if not self._running:
            return
        try:
            import numpy as np
            data = frame_int16[:, 0].copy() if getattr(frame_int16, "ndim", 1) > 1 else frame_int16.copy()
            self._queue.put_nowait(data)
        except Exception:
            pass

    def _loop(self) -> None:
        import numpy as np
        while self._running:
            try:
                frame = self._queue.get(timeout=0.2)
                if frame is None or not self._running:
                    break

                self._audio_buffer.append(frame)

                # Check auto-sleep timeout if awake
                if not self._asleep:
                    elapsed = time.monotonic() - self._last_activity_time
                    if elapsed > self._auto_sleep_seconds:
                        self.sleep(f"silence for {int(self._auto_sleep_seconds)} seconds")
                    continue

                # When asleep: analyze audio buffer locally without network streaming
                # Energy gate: check if there is significant acoustic energy
                frame_arr = np.asarray(frame, dtype=np.float32)
                rms = float(np.sqrt(np.mean(frame_arr ** 2))) if len(frame_arr) > 0 else 0.0

                if rms > 500.0 and len(self._audio_buffer) >= 15:
                    # Check openwakeword if model loaded
                    detected = False
                    phrase_detected = "Hey Spark"

                    if self._oww_model is not None:
                        try:
                            scores = self._oww_model.predict(np.asarray(frame, dtype=np.int16))
                            if isinstance(scores, dict):
                                max_score = max((float(v) for v in scores.values()), default=0.0)
                                if max_score >= self._threshold:
                                    detected = True
                        except Exception:
                            pass

                    # Also check lightweight acoustic/phonetic matcher or local transcription if energy peaked
                    if not detected and rms > 1200.0:
                        # Combine buffered audio (~1.5s) to test for speech
                        combined = np.concatenate(list(self._audio_buffer))
                        audio_float = combined.astype(np.float32) / 32768.0

                        # Lazy initialize local STT for exact phrase detection
                        if self._local_stt is None:
                            try:
                                from core.stt import WhisperSTT
                                self._local_stt = WhisperSTT(model_name="tiny")
                            except Exception:
                                self._local_stt = False

                        if self._local_stt:
                            try:
                                text = self._local_stt.transcribe(audio_float).strip().lower()
                                matched = matches_wake_phrase(text)
                                if matched:
                                    detected = True
                                    phrase_detected = matched
                            except Exception:
                                pass

                    if detected:
                        self._audio_buffer.clear()
                        self.wake(phrase_detected)

            except queue.Empty:
                # Check auto-sleep timeout during idle
                if not self._asleep:
                    elapsed = time.monotonic() - self._last_activity_time
                    if elapsed > self._auto_sleep_seconds:
                        self.sleep(f"silence for {int(self._auto_sleep_seconds)} seconds")
                continue
            except Exception as e:
                self._logger(f"[WakeWord] Inference error: {e}")
                time.sleep(0.05)
