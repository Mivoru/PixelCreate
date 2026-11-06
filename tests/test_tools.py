"""Run with: python tests/test_tools.py"""
import json
import sys
import unittest
from pathlib import Path

import numpy as np

sys.path.insert(0, str(Path(__file__).resolve().parent.parent / "tools"))

from lint import lint, stray_pixels, upscale_factor  # noqa: E402
from pixlib import PALETTE_DIR, Frame, from_hex, load_palette, luminance  # noqa: E402
from preview import build_sheet  # noqa: E402

RED = (200, 40, 40, 255)
BLUE = (40, 40, 200, 255)


def sprite(size=8):
    pixels = np.zeros((size, size, 4), np.uint8)
    pixels[2:6, 2:5] = RED
    pixels[3, 3] = BLUE
    return pixels


class LintTests(unittest.TestCase):
    def test_clean_sprite_passes(self):
        report = lint([Frame(sprite())], [RED[:3], BLUE[:3]], 2)
        self.assertEqual(report.errors, [])
        self.assertEqual(report.warnings, [])

    def test_semi_transparency_fails(self):
        pixels = sprite()
        pixels[2, 2, 3] = 128
        self.assertIn("semi-transparent", lint([Frame(pixels)], None, None).errors[0])

    def test_colour_outside_palette_fails(self):
        self.assertIn("#2828C8", lint([Frame(sprite())], [RED[:3]], None).errors[0])

    def test_colour_limit_fails(self):
        self.assertIn("limit is 1", lint([Frame(sprite())], None, 1).errors[0])

    def test_empty_sprite_fails(self):
        blank = np.zeros((4, 4, 4), np.uint8)
        self.assertEqual(lint([Frame(blank)], None, None).errors, ["sprite is empty"])

    def test_stray_pixel_found_at_true_position(self):
        pixels = sprite()
        pixels[7, 7] = RED
        self.assertEqual(stray_pixels(pixels[..., 3]), [(7, 7)])

    def test_edge_pixels_do_not_wrap_around(self):
        pixels = np.zeros((4, 4, 4), np.uint8)
        pixels[0, 0] = RED
        pixels[3, 3] = RED
        self.assertEqual(len(stray_pixels(pixels[..., 3])), 2)

    def test_upscaled_art_detected(self):
        big = np.repeat(np.repeat(sprite(), 3, 0), 3, 1)
        self.assertEqual(upscale_factor(big), 3)
        self.assertEqual(upscale_factor(sprite()), 1)


class PaletteTests(unittest.TestCase):
    def test_palette_sizes(self):
        for name, size in (("pico-8", 16), ("endesga-32", 32), ("resurrect-64", 64)):
            self.assertEqual(len(load_palette(name)), size, name)


class RampTests(unittest.TestCase):
    def test_ramps_use_palette_and_get_brighter(self):
        data = json.loads((PALETTE_DIR / "ramps-endesga-32.json").read_text(encoding="utf-8"))
        allowed = set(load_palette(data["palette"]))
        self.assertIn(from_hex(data["outline"]), allowed)
        for name, ramp in data["ramps"].items():
            colours = [from_hex(c) for c in ramp]
            self.assertTrue(set(colours) <= allowed, name)
            values = [luminance(c) for c in colours]
            self.assertEqual(values, sorted(values), name)


class PreviewTests(unittest.TestCase):
    def test_sheet_has_panel_for_each_frame_and_view(self):
        frames = [Frame(sprite()), Frame(sprite())]
        one = build_sheet(frames[:1], 4, "checker")
        self.assertGreater(build_sheet(frames, 4, "checker").width, one.width)


if __name__ == "__main__":
    unittest.main()
