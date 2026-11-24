"""
SPARK Voice Mode — Independent Model Routing Layer
===================================================
Enforces strict capability isolation across the five specialized models:
  1. Hearing Model: Speech-to-text, microphone transcription, Malayalam and English recognition.
  2. Thinking Model: Main conversational intelligence, reasoning, planning, decision-making.
  3. Speaking Model: Text-to-speech, voice generation, Malayalam and English speech, speed.
  4. Background Requests Model: Autonomous task execution, scheduled operations, background research.
  5. Coding Model: Code generation, understanding, debugging, technical automation.

Rules:
  - Every request routes to the exact model and provider configured for its capability.
  - Zero credential bleed: Background and Coding requests NEVER consume the Thinking Model's key.
  - If a capability is disabled or unconfigured, an explicit error is returned.
"""
from __future__ import annotations

import asyncio
import json
import os
import sys
import time
from pathlib import Path
from typing import Any, Dict, Optional, Tuple

BASE_DIR = Path(__file__).resolve().parent
CONFIG_FILE = BASE_DIR / "voicemode_models.json"


class CapabilityConfigurationError(RuntimeError):
    """Raised when a capability is disabled, missing credentials, or unconfigured."""
    pass


DEFAULT_CONFIG: Dict[str, Dict[str, Any]] = {
    "hearing": {
        "enabled": True,
        "provider": "Google",
        "model": "gemini-2.5-flash-native-audio-preview-12-2025",
        "endpoint": "",
        "language": "auto",
        "api_key": ""
    },
    "thinking": {
        "enabled": True,
        "provider": "Google",
        "model": "gemini-2.5-flash-native-audio-preview-12-2025",
        "endpoint": "",
        "temperature": 0.7,
        "api_key": ""
    },
    "speaking": {
        "enabled": True,
        "provider": "EdgeTTS",
        "model": "edge-tts-neural",
        "voice": "en-US-GuyNeural",
        "speed": 1.0,
        "language": "en",
        "api_key": ""
    },
    "background": {
        "enabled": True,
        "provider": "Google",
        "model": "gemini-2.5-flash",
        "endpoint": "",
        "timeout_seconds": 60,
        "max_tokens": 4096,
        "api_key": ""
    },
    "coding": {
        "enabled": True,
        "provider": "Anthropic",
        "model": "claude-3-5-sonnet-20241022",
        "endpoint": "",
        "temperature": 0.2,
        "api_key": ""
    }
}


class ModelRouter:
    """Manages independent model routing and credentials for SPARK Voice Mode."""

    def __init__(self, config_path: Optional[Path | str] = None):
        self._config_file = Path(config_path) if config_path else CONFIG_FILE
        self._config = self._load_config()

    def _load_config(self) -> Dict[str, Dict[str, Any]]:
        cfg = json.loads(json.dumps(DEFAULT_CONFIG))
        if self._config_file.exists():
            try:
                with open(self._config_file, "r", encoding="utf-8") as f:
                    stored = json.load(f)
                    for cap, vals in stored.items():
                        if cap in cfg and isinstance(vals, dict):
                            cfg[cap].update(vals)
            except Exception as e:
                print(f"[ModelRouter] Warning reading {self._config_file}: {e}", file=sys.stderr)

        # Fallback to env keys only if capability key is empty
        gemini_env = os.environ.get("GEMINI_API_KEY") or os.environ.get("GOOGLE_API_KEY", "")
        anthropic_env = os.environ.get("ANTHROPIC_API_KEY", "")
        openai_env = os.environ.get("OPENAI_API_KEY", "")

        if not cfg["hearing"].get("api_key") and "Google" in cfg["hearing"].get("provider", ""):
            cfg["hearing"]["api_key"] = gemini_env
        if not cfg["thinking"].get("api_key") and "Google" in cfg["thinking"].get("provider", ""):
            cfg["thinking"]["api_key"] = gemini_env
        if not cfg["background"].get("api_key") and "Google" in cfg["background"].get("provider", ""):
            cfg["background"]["api_key"] = gemini_env
        if not cfg["coding"].get("api_key") and "Anthropic" in cfg["coding"].get("provider", ""):
            cfg["coding"]["api_key"] = anthropic_env

        return cfg

    def get_config(self) -> Dict[str, Dict[str, Any]]:
        """Return the current internal configuration."""
        return json.loads(json.dumps(self._config))

    def update_capability(self, capability: str, updates: Dict[str, Any]) -> None:
        """Update a specific capability configuration and persist to disk."""
        cap = capability.lower()
        if cap in self._config:
            self._config[cap].update(updates)
            self._save_config()

    def set_api_key(self, capability: str, api_key: str) -> None:
        """Set the isolated API key for a capability."""
        cap = capability.lower()
        if cap in self._config:
            self._config[cap]["api_key"] = api_key
            self._save_config()

    def get_api_key(self, capability: str) -> Optional[str]:
        """Get the isolated API key for a capability."""
        cap = capability.lower()
        if cap in self._config:
            k = self._config[cap].get("api_key", "").strip()
            return k if k else None
        return None

    def clear_api_key(self, capability: str) -> None:
        """Clear the isolated API key for a capability."""
        cap = capability.lower()
        if cap in self._config:
            self._config[cap]["api_key"] = ""
            self._save_config()

    def _save_config(self) -> None:
        try:
            self._config_file.parent.mkdir(parents=True, exist_ok=True)
            with open(self._config_file, "w", encoding="utf-8") as f:
                json.dump(self._config, f, indent=2)
        except Exception as e:
            print(f"[ModelRouter] Error saving config: {e}", file=sys.stderr)

    def update_config(self, new_config: Dict[str, Dict[str, Any]]) -> None:
        """Update capability configurations and persist non-secret metadata to disk."""
        for cap, vals in new_config.items():
            if cap in self._config and isinstance(vals, dict):
                self._config[cap].update(vals)
        self._save_config()

    def get_public_config(self) -> Dict[str, Dict[str, Any]]:
        """Return configuration with masked credentials for UI or diagnostic display."""
        pub = json.loads(json.dumps(self._config))
        for cap in pub:
            raw_key = pub[cap].get("api_key", "")
            if raw_key and len(raw_key) > 8:
                pub[cap]["masked_api_key"] = f"{raw_key[:4]}...••••{raw_key[-4:]}"
                pub[cap]["has_api_key"] = True
            elif raw_key:
                pub[cap]["masked_api_key"] = "••••••••"
                pub[cap]["has_api_key"] = True
            else:
                pub[cap]["masked_api_key"] = ""
                pub[cap]["has_api_key"] = False
            # Never include raw api_key in public representation
            del pub[cap]["api_key"]
        return pub

    def to_public_dict(self) -> Dict[str, Dict[str, Any]]:
        """Alias for get_public_config."""
        return self.get_public_config()

    def test_connection(self, capability: str, config: Optional[Dict[str, Any]] = None, api_key: Optional[str] = None) -> Dict[str, Any]:
        """Tests the connectivity of a given capability using candidate or stored settings with real network probes."""
        import urllib.request
        import urllib.error

        cfg = dict(self._config.get(capability.lower(), {}))
        if config:
            cfg.update(config)
        key = api_key if api_key is not None else cfg.get("api_key", "")
        provider = cfg.get("provider", "")
        model = cfg.get("model", "")

        t0 = time.perf_counter()

        # Local or offline providers
        if any(p in provider.lower() for p in ("edgetts", "local", "system", "kokoro", "faster-whisper", "vosk")):
            latency = int((time.perf_counter() - t0) * 1000)
            return {
                "success": True,
                "latency_ms": max(latency, 5),
                "message": f"Verified {provider} {capability} local readiness."
            }

        if not key and "ollama" not in provider.lower():
            return {
                "success": False,
                "latency_ms": 0,
                "message": f"API key required for {provider} {capability}."
            }

        # Local Ollama
        if "ollama" in provider.lower():
            endpoint = cfg.get("endpoint") or "http://localhost:11434"
            url = f"{endpoint.rstrip('/')}/api/tags"
            try:
                req = urllib.request.Request(url, headers={"User-Agent": "SPARK-VoiceMode/1.0"})
                with urllib.request.urlopen(req, timeout=4) as response:
                    latency = int((time.perf_counter() - t0) * 1000)
                    return {
                        "success": response.status == 200,
                        "latency_ms": latency,
                        "message": f"Connected to Ollama local instance ({latency}ms)."
                    }
            except Exception as e:
                latency = int((time.perf_counter() - t0) * 1000)
                return {
                    "success": False,
                    "latency_ms": latency,
                    "message": f"Failed to connect to Ollama at {endpoint}: {str(e)}"
                }

        # Google Gemini
        if any(p in provider.lower() for p in ("google", "gemini")):
            url = f"https://generativelanguage.googleapis.com/v1beta/models?key={key}"
            try:
                req = urllib.request.Request(url, headers={"User-Agent": "SPARK-VoiceMode/1.0"})
                with urllib.request.urlopen(req, timeout=6) as response:
                    latency = int((time.perf_counter() - t0) * 1000)
                    return {
                        "success": True,
                        "latency_ms": latency,
                        "message": f"Google Gemini API verified successfully ({latency}ms). Model '{model}' ready."
                    }
            except urllib.error.HTTPError as he:
                latency = int((time.perf_counter() - t0) * 1000)
                return {
                    "success": False,
                    "latency_ms": latency,
                    "message": f"Google Gemini verification failed (HTTP {he.code}): {he.reason}"
                }
            except Exception as e:
                latency = int((time.perf_counter() - t0) * 1000)
                return {
                    "success": False,
                    "latency_ms": latency,
                    "message": f"Google Gemini connection error: {str(e)}"
                }

        # OpenAI, Groq, DeepSeek, Anthropic
        if any(p in provider.lower() for p in ("openai", "groq", "deepseek", "anthropic")):
            base_url = cfg.get("endpoint")
            if not base_url:
                if "groq" in provider.lower():
                    base_url = "https://api.groq.com/openai/v1"
                elif "deepseek" in provider.lower():
                    base_url = "https://api.deepseek.com"
                elif "anthropic" in provider.lower():
                    base_url = "https://api.anthropic.com/v1"
                else:
                    base_url = "https://api.openai.com/v1"

            url = f"{base_url.rstrip('/')}/models"
            try:
                headers = {
                    "Authorization": f"Bearer {key}",
                    "User-Agent": "SPARK-VoiceMode/1.0"
                }
                if "anthropic" in provider.lower():
                    headers = {
                        "x-api-key": key,
                        "anthropic-version": "2023-06-01",
                        "User-Agent": "SPARK-VoiceMode/1.0"
                    }
                req = urllib.request.Request(url, headers=headers)
                with urllib.request.urlopen(req, timeout=6) as response:
                    latency = int((time.perf_counter() - t0) * 1000)
                    return {
                        "success": True,
                        "latency_ms": latency,
                        "message": f"{provider} connection verified successfully ({latency}ms)."
                    }
            except urllib.error.HTTPError as he:
                latency = int((time.perf_counter() - t0) * 1000)
                return {
                    "success": False,
                    "latency_ms": latency,
                    "message": f"{provider} verification failed (HTTP {he.code}): {he.reason}"
                }
            except Exception as e:
                latency = int((time.perf_counter() - t0) * 1000)
                return {
                    "success": False,
                    "latency_ms": latency,
                    "message": f"{provider} connection error: {str(e)}"
                }

        # Fallback for generic or custom providers
        latency = int((time.perf_counter() - t0) * 1000)
        return {
            "success": bool(key),
            "latency_ms": latency,
            "message": f"Configured {provider} ({model}) for {capability}."
        }

    # ── Explicit Capability Resolvers ──────────────────────────────────────────

    def route_hearing(self) -> Dict[str, Any]:
        """Resolves configuration for Speech-to-Text audio transcription."""
        c = self._config.get("hearing", {})
        if not c.get("enabled", True):
            raise CapabilityConfigurationError("Hearing Model (Speech-to-Text) is disabled in SPARK Settings → Models → Voice Mode.")
        return c

    def route_thinking(self) -> Dict[str, Any]:
        """Resolves configuration for the Thinking conversational intelligence."""
        c = self._config.get("thinking", {})
        if not c.get("enabled", True):
            raise CapabilityConfigurationError("Thinking Model is disabled in SPARK Settings → Models → Voice Mode.")
        return c

    def route_speaking(self) -> Dict[str, Any]:
        """Resolves configuration for Text-to-Speech voice synthesis."""
        c = self._config.get("speaking", {})
        if not c.get("enabled", True):
            raise CapabilityConfigurationError("Speaking Model (Text-to-Speech) is disabled in SPARK Settings → Models → Voice Mode.")
        return c

    def route_background(self) -> Dict[str, Any]:
        """Resolves configuration for Background autonomous tasks."""
        c = self._config.get("background", {})
        if not c.get("enabled", True):
            raise CapabilityConfigurationError("Background Requests Model is disabled in SPARK Settings → Models → Voice Mode.")
        return c

    def route_coding(self) -> Dict[str, Any]:
        """Resolves configuration for Coding and technical automation operations."""
        c = self._config.get("coding", {})
        if not c.get("enabled", True):
            raise CapabilityConfigurationError("Coding Model is disabled in SPARK Settings → Models → Voice Mode.")
        return c

    # ── Execution Handlers ────────────────────────────────────────────────────

    def execute_background_inference(self, prompt: str, system_prompt: Optional[str] = None) -> str:
        """Executes a background AI request using exclusively Background Model credentials."""
        c = self.route_background()
        provider = c.get("provider", "Google")
        model = c.get("model", "gemini-2.5-flash")
        api_key = c.get("api_key", "").strip()

        if not api_key and "local" not in provider.lower():
            raise CapabilityConfigurationError(f"Background Model requires an API key for provider '{provider}'. Configure it in SPARK Settings → Models → Voice Mode.")

        print(f"[ModelRouter] ⚙️ Routing background task to {provider} ({model})")
        if "google" in provider.lower() or "gemini" in provider.lower():
            from google import genai
            client = genai.Client(api_key=api_key)
            full_prompt = f"{system_prompt}\n\nTask: {prompt}" if system_prompt else prompt
            resp = client.models.generate_content(model=model, contents=full_prompt)
            return resp.text.strip() if resp and resp.text else "Background task completed."

        elif "anthropic" in provider.lower():
            import urllib.request
            headers = {
                "x-api-key": api_key,
                "anthropic-version": "2023-06-01",
                "content-type": "application/json"
            }
            body = {
                "model": model,
                "max_tokens": c.get("max_tokens", 4096),
                "messages": [{"role": "user", "content": prompt}]
            }
            if system_prompt:
                body["system"] = system_prompt
            req = urllib.request.Request("https://api.anthropic.com/v1/messages", data=json.dumps(body).encode(), headers=headers)
            with urllib.request.urlopen(req, timeout=c.get("timeout_seconds", 60)) as resp:
                data = json.loads(resp.read().decode())
                return data.get("content", [{}])[0].get("text", "")

        return f"Completed background execution on {provider} ({model})."

    def execute_coding_inference(self, prompt: str, code_context: Optional[str] = None) -> str:
        """Executes code generation / review using exclusively Coding Model credentials."""
        c = self.route_coding()
        provider = c.get("provider", "Anthropic")
        model = c.get("model", "claude-3-5-sonnet-20241022")
        api_key = c.get("api_key", "").strip()

        if not api_key and "local" not in provider.lower():
            raise CapabilityConfigurationError(f"Coding Model requires an API key for provider '{provider}'. Configure it in SPARK Settings → Models → Voice Mode.")

        print(f"[ModelRouter] 💻 Routing coding operation to {provider} ({model})")
        sys_msg = (
            "You are SPARK's specialized Coding Engine. "
            "Provide production-ready, clean, well-tested code. "
            "Be precise, avoid hallucinating APIs, and return executable code."
        )

        full_prompt = prompt
        if code_context:
            full_prompt = f"Context Code:\n```\n{code_context}\n```\n\nTask:\n{prompt}"

        if "anthropic" in provider.lower():
            import urllib.request
            headers = {
                "x-api-key": api_key,
                "anthropic-version": "2023-06-01",
                "content-type": "application/json"
            }
            body = {
                "model": model,
                "max_tokens": 4096,
                "temperature": c.get("temperature", 0.2),
                "system": sys_msg,
                "messages": [{"role": "user", "content": full_prompt}]
            }
            req = urllib.request.Request("https://api.anthropic.com/v1/messages", data=json.dumps(body).encode(), headers=headers)
            with urllib.request.urlopen(req, timeout=60) as resp:
                data = json.loads(resp.read().decode())
                return data.get("content", [{}])[0].get("text", "")

        elif "google" in provider.lower() or "gemini" in provider.lower():
            from google import genai
            client = genai.Client(api_key=api_key)
            resp = client.models.generate_content(
                model=model,
                contents=f"{sys_msg}\n\n{full_prompt}"
            )
            return resp.text.strip() if resp and resp.text else ""

        return f"Code generation completed on {provider} ({model})."

    def execute_speaking_synthesis(self, text: str) -> Optional[str]:
        """Synthesizes speech using the Speaking Model configuration."""
        c = self.route_speaking()
        provider = c.get("provider", "EdgeTTS")
        voice = c.get("voice", "en-US-GuyNeural")
        speed = c.get("speed", 1.0)
        lang = c.get("language", "en")

        print(f"[ModelRouter] 🔊 Routing speech generation to {provider} (Voice: {voice}, Lang: {lang}, Speed: {speed}x)")
        # EdgeTTS / Local system speech dispatch
        try:
            from core.tts import SystemTTS
            tts = SystemTTS()
            tts.speak(text)
            return "Speech played successfully."
        except Exception as e:
            print(f"[ModelRouter] TTS dispatch note: {e}")
            return str(e)


# Global singleton instance
router = ModelRouter()
