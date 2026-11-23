import sys
import json
from typing import Dict, Any, Tuple

# Security guard state for the Python runtime
_ACTIVE_DIVISION = "voice-mode"
_ISOLATION_ENABLED = True

def set_active_division(division: str) -> None:
    global _ACTIVE_DIVISION
    _ACTIVE_DIVISION = division.strip().lower()

def get_active_division() -> str:
    return _ACTIVE_DIVISION

def is_voice_mode() -> bool:
    return _ACTIVE_DIVISION in ("voice-mode", "voicemode", "voice")

def guard_check(action_name: str) -> Tuple[bool, str]:
    """
    Validates whether the given action is allowed under the current division.
    If not in Voice Mode, rejects all MARK/Voice Mode capabilities.
    """
    if _ISOLATION_ENABLED and not is_voice_mode():
        msg = f"[VoiceModeCapabilityGuard] REJECTED: Tool '{action_name}' is restricted exclusively to activeDivision === 'voice-mode'. Current division: '{_ACTIVE_DIVISION}'."
        print(msg, file=sys.stderr)
        return False, msg
    return True, ""

# High-impact operations requiring explicit approval
HIGH_IMPACT_ACTIONS = {
    "delete_file",
    "remove_directory",
    "shutdown_spark",
    "restart_computer",
    "send_message",
    "destructive_terminal",
}

def is_high_impact(action_name: str) -> bool:
    return action_name.lower() in HIGH_IMPACT_ACTIONS
