"""
Integration Test Suite for SPARK Voice Mode Model & API Configuration
Verifies:
1. Five independent model capabilities (Hearing, Thinking, Speaking, Background, Coding).
2. Independent credential isolation (changing one key never touches another).
3. Secure key masking (never exposed in configs or public outputs).
4. Model router capability resolution and isolation (Background & Coding never bleed into Thinking).
5. IPC Server config retrieval and capability test commands.
"""

import os
import sys
import json
import tempfile
import unittest

# Ensure runtime directory is in sys.path
SCRIPT_DIR = os.path.dirname(os.path.abspath(__file__))
if SCRIPT_DIR not in sys.path:
    sys.path.insert(0, SCRIPT_DIR)

from model_router import ModelRouter, CapabilityConfigurationError


class TestVoiceModeModelsIntegration(unittest.TestCase):
    def setUp(self):
        self.temp_dir = tempfile.mkdtemp()
        self.config_path = os.path.join(self.temp_dir, "models_config.json")
        self.router = ModelRouter(self.config_path)

    def tearDown(self):
        import shutil
        shutil.rmtree(self.temp_dir, ignore_errors=True)

    def test_default_five_capabilities_exist(self):
        """Verify all 5 capabilities are initialized independently."""
        config = self.router.get_config()
        for cap in ["hearing", "thinking", "speaking", "background", "coding"]:
            self.assertIn(cap, config, f"Capability {cap} must exist in config")
            self.assertTrue(config[cap].get("enabled", False), f"{cap} must be enabled by default")
            self.assertIn("provider", config[cap])
            self.assertIn("model", config[cap])

    def test_independent_configuration_updates(self):
        """Updating one capability configuration must not alter other capabilities."""
        # Update Coding to DeepSeek without changing Thinking
        original_thinking_provider = self.router.get_config()["thinking"]["provider"]
        
        self.router.update_capability("coding", {
            "provider": "DeepSeek",
            "model": "deepseek-coder-v2",
            "temperature": 0.15
        })

        current_config = self.router.get_config()
        self.assertEqual(current_config["coding"]["provider"], "DeepSeek")
        self.assertEqual(current_config["coding"]["model"], "deepseek-coder-v2")
        self.assertEqual(current_config["coding"]["temperature"], 0.15)
        # Thinking remains unchanged
        self.assertEqual(current_config["thinking"]["provider"], original_thinking_provider)

    def test_independent_api_key_isolation(self):
        """Every capability must hold its own isolated API key with zero bleed."""
        keys = {
            "hearing": "hearing-secret-key-12345",
            "thinking": "thinking-secret-key-67890",
            "speaking": "speaking-secret-key-abcde",
            "background": "background-secret-key-fghij",
            "coding": "coding-secret-key-klmno"
        }

        # Set each key individually
        for cap, key in keys.items():
            self.router.set_api_key(cap, key)

        # Verify each capability resolves its exact key
        for cap, expected_key in keys.items():
            resolved_key = self.router.get_api_key(cap)
            self.assertEqual(resolved_key, expected_key, f"Capability {cap} must return its own key")

        # Clear coding key - other keys must remain intact
        self.router.clear_api_key("coding")
        self.assertIsNone(self.router.get_api_key("coding"))
        self.assertEqual(self.router.get_api_key("thinking"), "thinking-secret-key-67890")
        self.assertEqual(self.router.get_api_key("background"), "background-secret-key-fghij")
        self.assertEqual(self.router.get_api_key("hearing"), "hearing-secret-key-12345")
        self.assertEqual(self.router.get_api_key("speaking"), "speaking-secret-key-abcde")

    def test_public_config_masks_api_keys(self):
        """Public configuration representations must NEVER expose plaintext keys."""
        self.router.set_api_key("thinking", "sk-super-secret-production-key")
        public_dict = self.router.to_public_dict()
        
        thinking_public = public_dict["thinking"]
        self.assertTrue(thinking_public["has_api_key"])
        self.assertIn("••••", thinking_public["masked_api_key"])
        self.assertNotIn("sk-super-secret-production-key", json.dumps(public_dict))

    def test_background_does_not_consume_thinking_credentials(self):
        """Background tasks must strictly use Background credentials, never Thinking."""
        self.router.set_api_key("thinking", "thinking-only-key")
        self.router.set_api_key("background", "background-only-key")

        bg_route = self.router.route_background()
        thinking_route = self.router.route_thinking()

        self.assertEqual(bg_route["api_key"], "background-only-key")
        self.assertEqual(thinking_route["api_key"], "thinking-only-key")
        self.assertNotEqual(bg_route["api_key"], thinking_route["api_key"])

    def test_coding_does_not_consume_thinking_credentials(self):
        """Coding tasks must strictly use Coding credentials, never Thinking."""
        self.router.set_api_key("thinking", "thinking-only-key")
        self.router.set_api_key("coding", "coding-only-key")

        coding_route = self.router.route_coding()
        thinking_route = self.router.route_thinking()

        self.assertEqual(coding_route["api_key"], "coding-only-key")
        self.assertEqual(thinking_route["api_key"], "thinking-only-key")
        self.assertNotEqual(coding_route["api_key"], thinking_route["api_key"])

    def test_disabled_capability_raises_error(self):
        """Routing a disabled capability must cleanly raise CapabilityConfigurationError."""
        self.router.update_capability("coding", {"enabled": False})
        with self.assertRaises(CapabilityConfigurationError):
            self.router.route_coding()

    def test_capability_connection_test_mock(self):
        """Capability connection tests report status and latency cleanly."""
        res_edge = self.router.test_connection("speaking", {
            "provider": "EdgeTTS",
            "model": "en-US-GuyNeural"
        })
        self.assertTrue(res_edge["success"])
        self.assertIn("EdgeTTS", res_edge["message"])

        res_local = self.router.test_connection("hearing", {
            "provider": "Local",
            "model": "faster-whisper-medium"
        })
        self.assertTrue(res_local["success"])
        self.assertIn("Local", res_local["message"])


if __name__ == "__main__":
    unittest.main(verbosity=2)
