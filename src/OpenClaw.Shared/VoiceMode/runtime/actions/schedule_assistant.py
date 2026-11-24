"""
SPARK Voice Mode — Schedule & Calendar Assistant Action
========================================================
Integrates Voice Mode with SPARK's calendar, routines, and task-management infrastructure.
Supports:
  - Daily & weekly schedules
  - Study plans & work blocks
  - Meetings & events creation, rescheduling, cancellation
  - Personal routines & reminders
  - Reading agenda and calendar queries
"""
from __future__ import annotations

import json
import os
import sys
from datetime import datetime, timedelta
from pathlib import Path
from typing import Any, Dict, List, Optional

from core import outcome

def _base_dir() -> Path:
    if getattr(sys, "frozen", False):
        return Path(sys.executable).parent
    return Path(__file__).resolve().parent.parent

_SCHEDULE_FILE = _base_dir() / "memory" / "schedules.json"


def _load_events() -> List[Dict[str, Any]]:
    if _SCHEDULE_FILE.exists():
        try:
            with open(_SCHEDULE_FILE, "r", encoding="utf-8") as f:
                return json.load(f)
        except Exception:
            return []
    return []


def _save_events(events: List[Dict[str, Any]]) -> None:
    try:
        _SCHEDULE_FILE.parent.mkdir(parents=True, exist_ok=True)
        with open(_SCHEDULE_FILE, "w", encoding="utf-8") as f:
            json.dump(events, f, indent=2, ensure_ascii=False)
    except Exception as e:
        print(f"[ScheduleAssistant] Error saving schedule: {e}")


def schedule_assistant(
    parameters: dict,
    response=None,
    player=None,
    session_memory=None,
) -> str:
    """
    Handles scheduling, calendar planning, meetings, and routines.
    Actions:
      - add / create (meeting, event, task, routine)
      - list / get (view schedule for today, tomorrow, or date)
      - move / reschedule (update time/date of event)
      - cancel / delete (remove event)
      - plan_day / plan_study (create complete multi-block schedule)
    """
    params = parameters or {}
    action = str(params.get("action", "list")).strip().lower()
    title = str(params.get("title") or params.get("event") or params.get("task") or "").strip()
    date_str = str(params.get("date", "")).strip()
    time_str = str(params.get("time", "")).strip()
    category = str(params.get("category", "general")).strip()
    event_id = str(params.get("event_id", "")).strip()

    events = _load_events()

    if player and hasattr(player, "write_log"):
        player.write_log(f"[Schedule] Action: {action} ({title or date_str})")

    # ── Action: Add / Create ────────────────────────────────────────────────
    if action in ("add", "create", "schedule"):
        if not title:
            return outcome.failed("schedule_assistant", "Please provide a title or description for the scheduled item.")
        
        # Default to today if date not specified
        if not date_str:
            date_str = datetime.now().strftime("%Y-%m-%d")
        if not time_str:
            time_str = "09:00"

        eid = f"evt_{int(datetime.now().timestamp())}"
        new_event = {
            "id": eid,
            "title": title,
            "date": date_str,
            "time": time_str,
            "category": category,
            "status": "scheduled",
            "created_at": datetime.now().isoformat()
        }
        events.append(new_event)
        _save_events(events)

        # Mirror as desktop routine / reminder if suitable
        try:
            import actions.reminder as rem
            rem.reminder({"date": date_str, "time": time_str, "message": f"SPARK Schedule: {title}"}, player=player)
        except Exception:
            pass

        return outcome.done(
            "schedule_assistant",
            id=eid,
            event=title,
            date=date_str,
            time=time_str,
            category=category,
            status=f"Scheduled '{title}' for {date_str} at {time_str}."
        )

    # ── Action: List / Query ────────────────────────────────────────────────
    elif action in ("list", "get", "agenda", "query", "view"):
        target_date = date_str or datetime.now().strftime("%Y-%m-%d")
        
        # Check special words
        if date_str.lower() in ("today", ""):
            target_date = datetime.now().strftime("%Y-%m-%d")
        elif date_str.lower() == "tomorrow":
            target_date = (datetime.now() + timedelta(days=1)).strftime("%Y-%m-%d")

        matched = [e for e in events if e.get("date") == target_date and e.get("status") != "cancelled"]
        
        if matched:
            lines = [f"- [{e['time']}] {e['title']} ({e.get('category', 'general')}) [ID: {e['id']}]" for e in sorted(matched, key=lambda x: x.get("time", ""))]
            return f"Schedule for {target_date}:\n" + "\n".join(lines)
        else:
            return f"No scheduled events found for {target_date}."

    # ── Action: Reschedule / Move ───────────────────────────────────────────
    elif action in ("move", "reschedule", "update"):
        target_event = None
        if event_id:
            target_event = next((e for e in events if e.get("id") == event_id), None)
        elif title:
            target_event = next((e for e in events if title.lower() in e.get("title", "").lower() and e.get("status") != "cancelled"), None)

        if not target_event:
            return outcome.failed("schedule_assistant", f"Could not find an active event matching '{event_id or title}'.")

        if date_str:
            target_event["date"] = date_str
        if time_str:
            target_event["time"] = time_str
        _save_events(events)

        return outcome.done(
            "schedule_assistant",
            id=target_event["id"],
            event=target_event["title"],
            new_date=target_event["date"],
            new_time=target_event["time"],
            status=f"Moved '{target_event['title']}' to {target_event['date']} at {target_event['time']}."
        )

    # ── Action: Cancel / Delete ─────────────────────────────────────────────
    elif action in ("cancel", "delete", "remove"):
        target_event = None
        if event_id:
            target_event = next((e for e in events if e.get("id") == event_id), None)
        elif title:
            target_event = next((e for e in events if title.lower() in e.get("title", "").lower() and e.get("status") != "cancelled"), None)

        if not target_event:
            return outcome.failed("schedule_assistant", f"No scheduled item found matching '{event_id or title}'.")

        target_event["status"] = "cancelled"
        _save_events(events)

        return outcome.done(
            "schedule_assistant",
            id=target_event["id"],
            event=target_event["title"],
            status=f"Cancelled '{target_event['title']}' scheduled for {target_event['date']} at {target_event['time']}."
        )

    # ── Action: Plan Day / Study Plan ───────────────────────────────────────
    elif action in ("plan_day", "plan_study", "plan_week"):
        target_date = date_str or datetime.now().strftime("%Y-%m-%d")
        prompt = str(params.get("prompt") or params.get("instruction") or f"Create a comprehensive {action} for {target_date}").strip()
        
        try:
            import model_router
            plan = model_router.router.execute_background_inference(
                f"Create a structured daily schedule for {target_date} based on this request:\n{prompt}",
                "You are an expert productivity and schedule architect. Return a clear time-blocked plan."
            )
            return f"Plan for {target_date}:\n\n{plan}"
        except Exception:
            return (
                f"Generated Plan for {target_date}:\n"
                "- 09:00 - 10:30: Focus Session / Deep Work\n"
                "- 10:45 - 12:00: Tasks & Follow-ups\n"
                "- 13:00 - 14:30: Study / Project Review\n"
                "- 15:00 - 16:30: Collaboration & Action Items"
            )

    else:
        return outcome.failed("schedule_assistant", f"Unknown schedule action: '{action}'")


TOOL = {
    "name": "schedule_assistant",
    "description": (
        "Manages calendar events, study plans, meetings, routines, and daily/weekly schedules. "
        "Actions: add | list | move | cancel | plan_day | plan_study"
    ),
    "tier": "core",
    "parameters": {
        "type": "OBJECT",
        "properties": {
            "action": {
                "type": "STRING",
                "description": "add | list | move | cancel | plan_day | plan_study"
            },
            "title": {
                "type": "STRING",
                "description": "Title or description of the event or task"
            },
            "date": {
                "type": "STRING",
                "description": "Date in YYYY-MM-DD format (or 'today', 'tomorrow')"
            },
            "time": {
                "type": "STRING",
                "description": "Time in HH:MM format (24h)"
            },
            "category": {
                "type": "STRING",
                "description": "study | work | meeting | personal | routine"
            },
            "event_id": {
                "type": "STRING",
                "description": "ID of the event to reschedule or cancel"
            },
            "prompt": {
                "type": "STRING",
                "description": "Detailed instructions for generating study or daily plans"
            }
        },
        "required": ["action"]
    },
    "handler": schedule_assistant,
}
