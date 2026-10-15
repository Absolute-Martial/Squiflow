#!/usr/bin/env python3
"""Guard alias resolution, input bounds and equivalent Web/native token output."""
import importlib.util
import json
from pathlib import Path
import unittest
import xml.etree.ElementTree as ET

spec = importlib.util.spec_from_file_location("ui_tokens", Path(__file__).with_name("build-ui-tokens.py"))
tokens = importlib.util.module_from_spec(spec)
spec.loader.exec_module(tokens)


class TokenBuildTests(unittest.TestCase):
    def test_current_semantic_pairs_meet_text_and_control_contrast_thresholds(self):
        resolved = tokens.resolve_tokens(json.loads(tokens.SOURCE.read_text()))

        def luminance(name):
            color = resolved[f"color.{name}"][1].lstrip("#")
            channels = [int(color[index:index + 2], 16) / 255 for index in (0, 2, 4)]
            linear = [value / 12.92 if value <= 0.04045 else ((value + 0.055) / 1.055) ** 2.4 for value in channels]
            return sum(value * weight for value, weight in zip(linear, (0.2126, 0.7152, 0.0722)))

        pairs = [(foreground, background, 4.5) for foreground in ("text", "muted", "danger", "warning")
                 for background in ("page", "surface", "subtle")]
        pairs += [("onAction", background, 4.5) for background in ("action", "actionHover", "actionPressed")]
        pairs += [("control", background, 3) for background in ("page", "surface", "subtle")]
        pairs += [("focus", "page", 3), ("accent", "action", 3)]
        for foreground, background, minimum in pairs:
            with self.subTest(foreground=foreground, background=background):
                first, second = sorted((luminance(foreground), luminance(background)))
                self.assertGreaterEqual((second + 0.05) / (first + 0.05), minimum)

    def test_aliases_produce_matching_host_values_and_typed_native_dimensions(self):
        resolved = tokens.resolve_tokens({
            "palette": {"ink": {"$type": "color", "$value": "#20352e"}},
            "color": {"text": {"$type": "color", "$value": "{palette.ink}"}},
            "space": {"4": {"$type": "dimension", "$value": {"value": 16, "unit": "px"}}},
        })
        outputs = {path.name: value for path, value in tokens.generate(resolved, "main { gap: {{space.4}}; }").items()}
        self.assertIn("--color-text: #20352e;", outputs["tokens.css"])
        self.assertIn("gap: 16px", outputs["app.css"])
        native = ET.fromstring(outputs["DesignTokens.axaml"])
        resources = {node.attrib["{http://schemas.microsoft.com/winfx/2006/xaml}Key"]: node.text for node in native}
        self.assertEqual("#20352e", resources["Color.Text"])
        self.assertEqual("16", resources["Space.4.Inset"])
        self.assertEqual("16,0", resources["Space.4.Horizontal"])

    def test_missing_cyclic_and_mistyped_aliases_fail(self):
        cases = [
            {"a": {"$type": "color", "$value": "{missing}"}},
            {"a": {"$type": "color", "$value": "{b}"}, "b": {"$type": "color", "$value": "{a}"}},
            {"a": {"$type": "number", "$value": "{b}"}, "b": {"$type": "color", "$value": "#20352e"}},
        ]
        for document in cases:
            with self.subTest(document=document), self.assertRaises(ValueError):
                tokens.resolve_tokens(document)

    def test_invalid_values_fail_before_output(self):
        for kind, value in [("color", "red"), ("color", "#fff\"/>"), ("dimension", {"value": -1, "unit": "px"}),
                            ("dimension", {"value": 16, "unit": "rem"}), ("number", float("nan")), ("number", True),
                            ("number", 101), ("dimension", {"value": 4097, "unit": "px"})]:
            with self.subTest(kind=kind, value=value), self.assertRaises(ValueError):
                tokens.resolve_tokens({"a": {"$type": kind, "$value": value}})


if __name__ == "__main__":
    unittest.main()
