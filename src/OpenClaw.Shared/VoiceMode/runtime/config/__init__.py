import json, os
from pathlib import Path

_CONFIG_PATH = Path(__file__).parent / "api_keys.json"

def get_config() -> dict:
    cfg = {}
    if _CONFIG_PATH.exists():
        try:
            with open(_CONFIG_PATH, "r", encoding="utf-8") as f:
                cfg = json.load(f)
        except Exception:
            cfg = {}
    
    # Environment variable fallbacks for SPARK / system integration
    if "gemini_api_key" not in cfg or not cfg["gemini_api_key"]:
        env_gemini = os.environ.get("GEMINI_API_KEY") or os.environ.get("GOOGLE_API_KEY")
        if env_gemini:
            cfg["gemini_api_key"] = env_gemini

    if "openrouter_api_key" not in cfg or not cfg["openrouter_api_key"]:
        env_or = os.environ.get("OPENROUTER_API_KEY")
        if env_or:
            cfg["openrouter_api_key"] = env_or

    if "os_system" not in cfg:
        cfg["os_system"] = "windows"

    return cfg

def get_os() -> str:
    """Returns: 'windows' | 'mac' | 'linux'"""
    return get_config().get("os_system", "windows").lower()

def is_windows() -> bool: return get_os() == "windows"
def is_mac()     -> bool: return get_os() == "mac"
def is_linux()   -> bool: return get_os() == "linux"
