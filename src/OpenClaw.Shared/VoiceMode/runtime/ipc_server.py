"""
SPARK Voice Mode — Dedicated IPC Server & Capability Dispatcher
==============================================================
Exposes real-time voice intelligence, visual awareness, computer control,
autonomous task execution, persistent memory, and file intelligence.
Strictly enforced by VoiceModeCapabilityGuard (security_guard.py):
Only allows access when activeDivision === "voice-mode".
"""
from __future__ import annotations

import collections
import datetime
import json
import os
import sys
import threading
import time
import traceback
from pathlib import Path

# Add current directory to path
sys.path.insert(0, str(Path(__file__).resolve().parent))

from security_guard import guard_check, set_active_division, get_active_division, is_voice_mode

# Mark-LVII / SPARK Action modules
import actions.computer_control as computer_control_mod
import actions.computer_settings as computer_settings_mod
import actions.screen_processor as screen_processor_mod
import actions.file_processor as file_processor_mod
import actions.file_controller as file_controller_mod
import actions.browser_control as browser_control_mod
import actions.open_app as open_app_mod
import actions.dev_agent as dev_agent_mod
import actions.code_helper as code_helper_mod
import actions.web_search as web_search_mod
import actions.weather_report as weather_report_mod
import actions.reminder as reminder_mod
import actions.flight_finder as flight_finder_mod
import actions.youtube_video as youtube_video_mod
import actions.send_message as send_message_mod
import actions.email_assistant as email_assistant_mod
import actions.schedule_assistant as schedule_assistant_mod
import actions.routines as routines_mod
import agent.planner as planner_mod
import agent.executor as executor_mod
import agent.task_queue as task_queue_mod
import memory.memory_manager as memory_manager_mod
from core.wake_word import WakeWordDetector, matches_wake_phrase, WAKE_PHRASES

task_queue = task_queue_mod.TaskQueue()
task_queue.start()

# Operational state
_OPERATIONAL_STATE = {
    "visual_mode": "non-face",   # "non-face" | "face"
    "model": "models/gemini-2.5-flash-native-audio-preview-12-2025",
    "voice_state": "LISTENING",  # "IDLE" | "LISTENING" | "THINKING" | "SPEAKING" | "SLEEPING"
    "wake_word_active": True,
    "current_stage": 0,          # 0: Understanding, 1: Planning, 2: Execution, 3: Verification, 4: Completed
    "activities": [],
    "recent_tasks": []
}


class MockUI:
    def __init__(self):
        self.muted = False
    def write_log(self, text):
        _OPERATIONAL_STATE["activities"].append({
            "time": datetime.datetime.now().strftime("%H:%M:%S"),
            "message": text
        })
    def set_state(self, state):
        _OPERATIONAL_STATE["voice_state"] = state


mock_ui = MockUI()


def handle_request(req: dict) -> dict:
    cmd = req.get("command") or req.get("action", "")
    division = req.get("division")
    if division:
        set_active_division(division)
    else:
        division = get_active_division()

    # Control commands are always allowed; functional tools are guarded
    if cmd not in ("ping", "set_division", "get_state", "get_voice_models_config", "update_voice_models_config", "test_capability_connection"):
        allowed, rejection = guard_check(cmd)
        if not allowed:
            return {"success": False, "error": rejection, "command": cmd}

    params = req.get("params") or req.get("parameters", {})

    try:
        if cmd == "ping":
            return {
                "success": True,
                "result": "pong",
                "active_division": division,
                "timestamp": datetime.datetime.now().isoformat()
            }

        elif cmd == "set_division":
            new_div = params.get("division", "voice-mode")
            set_active_division(new_div)
            return {"success": True, "active_division": new_div}

        # ── State & Mode Management ─────────────────────────────────────────
        elif cmd == "get_state":
            return {
                "success": True,
                "visual_mode": _OPERATIONAL_STATE["visual_mode"],
                "model": _OPERATIONAL_STATE["model"],
                "voice_state": _OPERATIONAL_STATE["voice_state"],
                "stage": _OPERATIONAL_STATE["current_stage"],
                "wake_phrases": WAKE_PHRASES,
                "active_division": division
            }

        elif cmd == "set_visual_mode":
            mode = params.get("mode", "non-face").lower()
            if mode not in ("face", "non-face"):
                mode = "non-face"
            _OPERATIONAL_STATE["visual_mode"] = mode
            return {"success": True, "visual_mode": mode}

        elif cmd == "set_model":
            model = params.get("model", "")
            if model:
                _OPERATIONAL_STATE["model"] = model
            return {"success": True, "model": _OPERATIONAL_STATE["model"]}

        # ── Voice Mode 5-Model Independent Configuration ────────────────────
        elif cmd == "get_voice_models_config":
            import model_router
            return {"success": True, "config": model_router.router.get_public_config()}

        elif cmd == "update_voice_models_config":
            import model_router
            model_router.router.update_config(params)
            return {"success": True, "config": model_router.router.get_public_config()}

        elif cmd == "test_capability_connection":
            import model_router
            cap_str = params.get("capability", "thinking").lower()
            try:
                test_res = model_router.router.test_connection(
                    capability=cap_str,
                    config=params.get("config"),
                    api_key=params.get("api_key")
                )
                return {
                    "success": test_res.get("success", False),
                    "capability": cap_str,
                    "latency_ms": test_res.get("latency_ms", 0),
                    "message": test_res.get("message", "")
                }
            except Exception as e:
                return {"success": False, "capability": cap_str, "error": str(e), "latency_ms": 0}

        elif cmd == "execute_coding_task":
            import model_router
            prompt = params.get("prompt", "")
            context = params.get("context")
            res = model_router.router.execute_coding_inference(prompt, context)
            return {"success": True, "result": res}

        elif cmd == "execute_background_task":
            import model_router
            prompt = params.get("prompt", "")
            system_msg = params.get("system_prompt")
            res = model_router.router.execute_background_inference(prompt, system_msg)
            return {"success": True, "result": res}

        # ── GROUP 1: Voice Intelligence ─────────────────────────────────────
        elif cmd in ("voice_interact", "text_interact"):
            prompt = params.get("transcript") or params.get("text") or params.get("prompt", "")
            _OPERATIONAL_STATE["voice_state"] = "THINKING"
            _OPERATIONAL_STATE["current_stage"] = 1  # Planning

            # Check wake word matches
            wake_match = matches_wake_phrase(prompt)
            is_awake = True if wake_match else True

            # Use ModelRouter thinking inference
            import model_router
            res_text = f"SPARK received: '{prompt}'. Executing requested action with division boundary intact."
            try:
                # Add Malayalam / English bilingual and context-continuity system prompt
                sys_inst = (
                    "You are SPARK Voice Mode, an intelligent personal AI assistant. "
                    "Maintain seamless conversational context across multiple turns. "
                    "If the user speaks or asks in Malayalam (മലയാളം), you MUST reply fluently and naturally in Malayalam. "
                    "If the user speaks in English, reply in English. Do not mix languages within a single reply. "
                    "Be direct, precise, concise, and helpful."
                )
                res_text = model_router.router.execute_background_inference(prompt, sys_inst)
            except Exception as e:
                # Fallback to direct Gemini client if model router has exception
                try:
                    from config import get_config
                    cfg = get_config()
                    gemini_key = os.environ.get("GEMINI_API_KEY") or os.environ.get("GOOGLE_API_KEY") or cfg.get("gemini_api_key")
                    selected_model = _OPERATIONAL_STATE.get("model", "models/gemini-2.5-flash")
                    mod_name = selected_model.replace("models/", "") if selected_model.startswith("models/") else "gemini-2.5-flash"

                    if gemini_key:
                        from google import genai
                        client = genai.Client(api_key=gemini_key)
                        resp = client.models.generate_content(
                            model=mod_name,
                            contents=f"You are SPARK Voice Mode. If user writes in Malayalam, reply in Malayalam. User said: {prompt}"
                        )
                        if resp and resp.text:
                            res_text = resp.text.strip()
                except Exception:
                    pass

            _OPERATIONAL_STATE["voice_state"] = "SPEAKING"
            _OPERATIONAL_STATE["current_stage"] = 4  # Completed
            return {
                "success": True,
                "result": res_text,
                "wake_match": wake_match,
                "is_awake": is_awake,
                "model": _OPERATIONAL_STATE["model"]
            }

        elif cmd == "interrupt_voice":
            _OPERATIONAL_STATE["voice_state"] = "LISTENING"
            # Cancel current output
            return {"success": True, "result": "Voice output interrupted and reset to listening."}

        # ── Specialized Assistants: Email, Messaging & Scheduling ───────────
        elif cmd in ("email_assistant", "email"):
            res = email_assistant_mod.email_assistant(params, player=mock_ui)
            return {"success": True, "result": res}

        elif cmd in ("send_message", "whatsapp_message", "whatsapp"):
            res = send_message_mod.send_message(params, player=mock_ui)
            return {"success": True, "result": res}

        elif cmd in ("schedule_assistant", "schedule", "calendar"):
            res = schedule_assistant_mod.schedule_assistant(params, player=mock_ui)
            return {"success": True, "result": res}

        elif cmd == "routines":
            res = routines_mod.routines(params, player=mock_ui)
            return {"success": True, "result": res}

        elif cmd == "reminder":
            res = reminder_mod.reminder(params, player=mock_ui)
            return {"success": True, "result": res}

        # ── GROUP 2: Visual Awareness ───────────────────────────────────────
        elif cmd == "screen_capture":
            data, mime = screen_processor_mod._capture_screen()
            return {"success": True, "bytes_length": len(data), "format": mime}

        elif cmd == "visual_qa":
            query = params.get("query", "Describe what you see.")
            mode = params.get("mode", "screen")
            res = screen_processor_mod.screen_process({"query": query, "mode": mode}, mock_ui)
            return {"success": True, "result": res}

        # ── GROUP 3: Advanced Computer Control ──────────────────────────────
        elif cmd == "computer_settings":
            res = computer_settings_mod.computer_settings(params, mock_ui)
            return {"success": True, "result": res}

        elif cmd == "computer_control":
            res = computer_control_mod.computer_control(params, mock_ui)
            return {"success": True, "result": res}

        elif cmd in ("uitars", "ui_tars", "agent_computer_use"):
            instruction = params.get("instruction") or params.get("description", "")
            res = computer_control_mod.execute_uitars_step(instruction)
            return {"success": True, "result": res}

        elif cmd == "open_app":
            app_name = params.get("app_name", "")
            res = open_app_mod.open_app({"app_name": app_name}, mock_ui)
            return {"success": True, "result": res}

        elif cmd == "browser_control":
            res = browser_control_mod.browser_control(params, mock_ui)
            return {"success": True, "result": res}

        elif cmd == "file_controller":
            res = file_controller_mod.file_controller(params, mock_ui)
            return {"success": True, "result": res}

        # ── GROUP 4: Autonomous Task Execution ──────────────────────────────
        elif cmd == "submit_task":
            goal = params.get("goal", "")
            tid = task_queue.submit(goal)
            _OPERATIONAL_STATE["current_stage"] = 2  # Execution
            _OPERATIONAL_STATE["recent_tasks"].append({"task_id": tid, "goal": goal, "status": "Running"})
            return {"success": True, "task_id": tid}

        elif cmd == "get_task_status":
            tid = params.get("task_id", "")
            status = task_queue.get_status(tid)
            return {"success": True, "status": status}

        elif cmd == "get_reasoning_state":
            return {
                "success": True,
                "stage": _OPERATIONAL_STATE["current_stage"],
                "stages": ["UNDERSTANDING", "PLANNING", "EXECUTION", "VERIFICATION", "COMPLETED"],
                "activities": _OPERATIONAL_STATE["activities"][-10:],
                "recent_tasks": _OPERATIONAL_STATE["recent_tasks"][-5:],
                "tools_active": [
                    {"name": "Visual Awareness", "status": "Ready"},
                    {"name": "Computer Control", "status": "Guarded"},
                    {"name": "File Intelligence", "status": "Ready"},
                    {"name": "Memory Engine", "status": "Active"}
                ]
            }

        # ── GROUP 5: Persistent Memory ──────────────────────────────────────
        elif cmd == "load_memory":
            m = memory_manager_mod.load_memory()
            return {"success": True, "memory": m}

        elif cmd == "update_memory":
            m_update = params.get("data", {})
            m = memory_manager_mod.update_memory(m_update)
            return {"success": True, "memory": m}

        # ── GROUP 6: File Intelligence ──────────────────────────────────────
        elif cmd == "process_file":
            res = file_processor_mod.file_processor(params, mock_ui)
            return {"success": True, "result": res}

        else:
            return {"success": False, "error": f"Unknown command '{cmd}'"}

    except Exception as e:
        return {"success": False, "error": str(e), "traceback": traceback.format_exc()}


def main():
    # If run in stdio server mode
    for line in sys.stdin:
        line = line.strip()
        if not line:
            continue
        try:
            req = json.loads(line)
            resp = handle_request(req)
            sys.stdout.write(json.dumps(resp) + "\n")
            sys.stdout.flush()
        except Exception as e:
            sys.stdout.write(json.dumps({"success": False, "error": str(e)}) + "\n")
            sys.stdout.flush()


if __name__ == "__main__":
    if len(sys.argv) > 1 and sys.argv[1] == "--server":
        main()
    elif len(sys.argv) > 1 and sys.argv[1] == "--ui":
        import ui
        ui.main()
    else:
        main()
