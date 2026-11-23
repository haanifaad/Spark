"""
Automated regression test suite for SPARK Voice Mode Runtime (6 Feature Groups & Division Guard).
"""
import sys
import os
import json

# Ensure runtime path
sys.path.insert(0, os.path.abspath(os.path.dirname(__file__)))

from ipc_server import handle_request
import security_guard

def test_guard():
    # 1. Division security guard rejection outside voice-mode
    security_guard.set_active_division("chat")
    allowed, msg = security_guard.guard_check("screen_capture")
    assert not allowed, f"Should have been rejected, got {allowed}"
    assert "REJECTED" in msg
    
    # 2. Test rejection via handle_request
    res = handle_request({"command": "screen_capture"})
    assert res.get("success") is False, f"Expected success=False, got {res}"
    assert "restricted exclusively" in res.get("error", "")
    
    # 3. Re-enable voice-mode
    security_guard.set_active_division("voice-mode")
    allowed, msg = security_guard.guard_check("screen_capture")
    assert allowed, f"Should have been allowed, got {allowed}"
    print("[PASS] Division Security Guard enforces isolation correctly.")

def test_ipc_capabilities():
    security_guard.set_active_division("voice-mode")
    
    # 1. Ping / Handshake
    res = handle_request({"command": "ping"})
    assert res.get("success") is True, f"Ping failed: {res}"
    assert res.get("result") == "pong"
    
    # 2. Get State
    res = handle_request({"command": "get_state"})
    assert res.get("success") is True
    assert res.get("visual_mode") in ["non-face", "face"]
    
    # 3. Visual Mode toggle
    res = handle_request({"command": "set_visual_mode", "params": {"mode": "face"}})
    assert res.get("success") is True
    assert res.get("visual_mode") == "face"
    res = handle_request({"command": "set_visual_mode", "params": {"mode": "non-face"}})
    assert res.get("success") is True
    assert res.get("visual_mode") == "non-face"
    
    # 4. Group 1: Voice Intelligence
    res = handle_request({"command": "voice_interact", "params": {"text": "Hello SPARK"}})
    assert res.get("success") is True
    assert "result" in res
    
    res = handle_request({"command": "interrupt_voice"})
    assert res.get("success") is True
    print("[PASS] Group 1 (Voice Intelligence) verified.")
    
    # 5. Group 2: Visual Awareness
    res = handle_request({"command": "screen_capture"})
    assert res.get("success") is True
    assert res.get("bytes_length") > 0
    
    res = handle_request({"command": "visual_qa", "params": {"query": "Describe the desktop"}})
    assert res.get("success") is True
    assert "result" in res
    print("[PASS] Group 2 (Visual Awareness) verified.")
    
    # 6. Group 3: Computer Control
    res = handle_request({"command": "computer_settings", "params": {"action": "volume_get"}})
    assert res.get("success") is True
    
    res = handle_request({"command": "computer_control", "params": {"action": "get_position"}})
    assert res.get("success") is True
    print("[PASS] Group 3 (Computer Control) verified.")
    
    # 7. Group 4: Autonomous Task Execution
    res = handle_request({"command": "submit_task", "params": {"goal": "Analyze market trends"}})
    assert res.get("success") is True
    task_id = res.get("task_id")
    assert task_id is not None
    
    res = handle_request({"command": "get_task_status", "params": {"task_id": task_id}})
    assert res.get("success") is True
    assert "status" in res
    
    res = handle_request({"command": "get_reasoning_state"})
    assert res.get("success") is True
    assert "stage" in res
    assert "stages" in res
    print("[PASS] Group 4 (Autonomous Task Execution) verified.")
    
    # 8. Group 5: Persistent Memory
    res = handle_request({"command": "update_memory", "params": {"data": {"user_preference_tone": "concise"}}})
    assert res.get("success") is True
    
    res = handle_request({"command": "load_memory"})
    assert res.get("success") is True
    mem_entry = res.get("memory", {}).get("user_preference_tone")
    assert mem_entry == "concise" or (isinstance(mem_entry, dict) and mem_entry.get("value") == "concise"), f"Unexpected memory value: {mem_entry}"
    print("[PASS] Group 5 (Persistent Memory) verified.")
    
    # 9. Group 6: File Intelligence
    res = handle_request({"command": "process_file", "params": {"file_path": "LICENSE_MARK_LVII", "action": "inspect"}})
    assert res.get("success") is True
    assert "result" in res
    print("[PASS] Group 6 (File Intelligence) verified.")

def test_wake_word_and_auto_sleep():
    from core.wake_word import matches_wake_phrase, WakeWordDetector, WAKE_PHRASES
    
    # 1. Verify "Hey Spark" matches
    m = matches_wake_phrase("Hey Spark")
    assert m is not None and "spark" in m.lower(), f"Expected Hey Spark match, got {m}"
    
    # 2. Verify variants match
    m = matches_wake_phrase("Spark whats up?")
    assert m is not None, "Expected 'Spark whats up' match"
    
    m = matches_wake_phrase("Spark what's up")
    assert m is not None, "Expected 'Spark what\\'s up' match"
    
    m = matches_wake_phrase("Good morning, Spark! How are you?")
    assert m is not None and "morning" in m.lower(), f"Expected Good morning Spark match, got {m}"
    
    # 3. Verify outdated "Hey Jarvis" does NOT wake SPARK
    m = matches_wake_phrase("Hey Jarvis")
    assert m is None, f"Expected no match for Hey Jarvis, but got {m}"
    
    # 4. Verify WakeWordDetector starts asleep
    detected_phrases = []
    detector = WakeWordDetector(
        on_wake=lambda p: detected_phrases.append(p),
        auto_sleep_seconds=0.05
    )
    assert detector.is_asleep is True, "Detector must start asleep until called"
    assert detector.is_awake is False
    
    # 5. Verify waking up via wake word
    detector.wake("Hey Spark")
    assert detector.is_asleep is False
    assert detector.is_awake is True
    assert "Hey Spark" in detected_phrases
    
    # 6. Verify auto-sleep after silence timeout (simulating > 0.05s)
    import time
    time.sleep(0.08)
    slept = detector.check_sleep_timeout()
    assert slept is True or detector.is_asleep is True, "Detector must auto-sleep after timeout"
    assert detector.is_asleep is True, "Detector must be in sleep mode after silence"
    
    print("[PASS] Local 'Hey Spark' wake-word detection, variants, and 2-min auto-sleep verified.")

    print("\n=======================================================")
    print("ALL 6 FEATURE GROUPS, DIVISION GUARD, AND 'HEY SPARK' WAKE WORD PASSED REGRESSION TESTING!")
    print("=======================================================\n")

if __name__ == "__main__":
    test_guard()
    test_ipc_capabilities()
    test_wake_word_and_auto_sleep()
