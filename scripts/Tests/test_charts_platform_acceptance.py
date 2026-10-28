"""Contradictory-evidence tests, not native-platform or product certification."""
from __future__ import annotations

import json
from copy import deepcopy
import os
import shutil
import subprocess
from pathlib import Path
import sys
import tempfile
import unittest

SCRIPTS = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(SCRIPTS))
from charts_png_oracle import _write_png, sha_bgra  # noqa: E402
from charts_platform_acceptance_oracle import deterministic_scene_checks, glyph_orientation_evidence, inspect_case, inspect_run, load, logical_display_protocol, outside_region_changes, same_manifest, sha  # noqa: E402


MODULES = ("Charts.PlatformAcceptance.dll", "NT.AvaloniaControl.Charts.dll", "NT.AvaloniaControl.Charts.Themes.dll", "Avalonia.Base.dll", "Avalonia.Skia.dll")
HOST_CHECKS = ("native-visible-window", "observed-native-backend", "native-render-surface-observed", "software-request-observed-on-native-surface", "gpu-context-request-observed-on-native-surface", "requested-native-scale-observed", "current-culture-matches-request", "suite-has-positive-coverage")


def temporary_workspace():
    # Keep fixture creation/removal inside the declared workspace, including on
    # Windows restricted runners whose ambient system TEMP is not writable.
    base = (SCRIPTS.parent / "artifacts/charts-platform-oracle-selftests").resolve()
    base.mkdir(parents=True, exist_ok=True)
    result = tempfile.TemporaryDirectory(prefix="owned-", dir=base)
    assert Path(result.name).resolve().is_relative_to(base)
    return result


class EvidenceFixture:
    def __init__(self, root):
        self.root = Path(root)
        self.root.mkdir(parents=True, exist_ok=True)
        self.case = {"id": "software-fixture", "renderer": "software", "culture": "zh-CN", "theme": "dark", "scenario": "desktop", "expectedScale": 1.25, "processScreenScale": 1.25}
        self.contract = {"hostSchema": "charts-platform-acceptance/v1", "requiredHostCheckKeys": list(HOST_CHECKS), "requiredCheckKeys": ["fixture-native-contract-" + str(i) for i in range(8)], "minimumRasterPairs": {"pngEqualPairs": 1, "pngDifferentPairs": 1}, "minimumGlyphProofs": 1}
        self.assemblies = [{"path": name, "sha256": "A" * 64, "bytes": 10} for name in MODULES]
        plain = b"\x00\x00\x00\xff" * 64
        changed = bytearray(plain)
        for offset in (0, 4, 8, 12):
            changed[offset:offset + 3] = b"\xff\x00\x00"
        self.artifacts = []
        for name, pixels in (("baseline.png", plain), ("hidden.png", bytes(changed)), ("restored.png", plain)):
            path = self.root / name
            _write_png(path, 8, 8, pixels)
            self.artifacts.append({"path": name, "kind": "png", "purpose": "validator-fixture-only", "pixelWidth": 8, "pixelHeight": 8, "sha256": sha(path), "decodedBgraSha256": sha_bgra(pixels)})
        self.raw = {
            "schema": "charts-platform-acceptance/v1", "passed": True, "status": "completed", "checks": {key: True for key in HOST_CHECKS},
            "options": {"renderer": "software", "culture": "zh-CN", "theme": "dark", "scenario": "desktop", "requestedScale": 1.25, "interactive": False},
            "qualification": {"physicalInputValidated": False, "compositorPresentValidated": False},
            "environmentAfter": {
                "window": {"actualBackend": "Win32", "backendType": "Avalonia.Win32.WindowImpl", "handleDescriptor": "HWND", "handle": "0xABC123", "isVisible": True, "renderScaling": 1.25, "clientSize": {"width": 100, "height": 80}, "physicalClientSize": {"width": 125, "height": 100}, "win32ReportedMetrics": {"clientRectAvailable": True, "clientRect": {"width": 125, "height": 100}, "dpiForWindow": 120, "windowDpiAwareness": 2}},
                "runtime": {"osDescription": "Microsoft Windows 10.0.26200", "osArchitecture": "X64", "processArchitecture": "X64", "processId": 1234},
                "screens": [{"displayName": "fixture-only"}],
                "culture": {"currentCulture": "zh-CN", "currentUiCulture": "zh-CN"},
                "rendering": {"actualRenderer": "software-raster", "hardwareGpuProven": False, "nativeWindowSurfaceObservations": [{"hasGrContext": False, "epoch": 1, "phase": "before-suite", "error": None}, {"hasGrContext": False, "epoch": 2, "phase": "after-suite", "error": None}]},
                "assemblies": [{"path": "/fixture/" + name, "sha256": "A" * 64} for name in MODULES],
            },
            "suite": {"checks": {key: True for key in self.contract["requiredCheckKeys"]}, "faults": [], "artifacts": self.artifacts,
                      "observations": [{"kind": "oracle", "oracle": {"pngEqualPairs": [{"firstPath": "baseline.png", "secondPath": "restored.png"}], "pngDifferentPairs": [{"firstPath": "baseline.png", "secondPath": "hidden.png"}], "glyphProofs": [{"baselinePath": "baseline.png", "hiddenPath": "hidden.png", "restoredPath": "restored.png", "pixelRegion": {"x": 0, "y": 0, "width": 4, "height": 2}}]}}]},
        }
        self.raw["environmentBefore"] = deepcopy(self.raw["environmentAfter"])

    def save(self):
        (self.root / "raw.json").write_text(json.dumps(self.raw), encoding="utf-8")

    def inspect(self):
        self.save()
        return inspect_case(self.root, self.case, self.contract, self.assemblies, expected_platform="windows", expected_architecture="X64")


class NativeEvidenceOracleTests(unittest.TestCase):
    def setUp(self):
        self.temp = temporary_workspace()
        self.fixture = EvidenceFixture(Path(self.temp.name) / "case")

    def tearDown(self):
        self.temp.cleanup()

    def assert_rejected(self):
        try:
            result = self.fixture.inspect()
        except (ValueError, KeyError, TypeError, OSError):
            return
        self.assertFalse(result["passed"], result)

    def test_consistent_fixture_passes_without_physical_claim(self):
        result = self.fixture.inspect()
        self.assertTrue(result["passed"], result)
        self.assertEqual("software-raster", result["actualRenderer"])
        self.assertEqual(4, result["glyphProofs"][0]["hiddenChangedPixels"])

    def test_unrequested_native_text_isolation_cannot_be_hidden(self):
        self.fixture.raw["options"]["isolateNativeText"] = True
        self.assert_rejected()

    def test_windows_cannot_claim_linux_native_text_isolation(self):
        self.fixture.case["isolateNativeText"] = True
        self.fixture.raw["options"]["isolateNativeText"] = True
        self.assert_rejected()

    def test_false_options_cannot_hide_applied_native_text_binding(self):
        self.fixture.raw["nativeTextIsolation"] = {"requested": False, "applied": True, "resolverRegistered": True, "resolverObserved": True, "flags": 10}
        self.assert_rejected()

    def test_inactive_binding_cannot_claim_ordinary_consumer_certification(self):
        self.fixture.raw["nativeTextIsolation"] = {"requested": False, "applied": False, "resolverRegistered": False, "resolverObserved": False, "flags": 0, "hostOnly": True, "ordinaryConsumerNativeTextCertified": True}
        self.assert_rejected()

    def inspect_linux_scale(self):
        self.fixture.save()
        return inspect_case(self.fixture.root, self.fixture.case, self.fixture.contract, self.fixture.assemblies, expected_platform="linux", expected_architecture="X64")

    def prepare_linux_scale(self):
        for phase in ("environmentBefore", "environmentAfter"):
            observed = self.fixture.raw[phase]
            observed["window"].update(actualBackend="X11", backendType="Avalonia.X11.WindowImpl", handleDescriptor="XID")
            observed["runtime"]["osDescription"] = "Ubuntu 26.04 LTS"
            observed["session"] = {"globalScaleFactor": "1.25", "screenScaleFactors": None}

    def prepare_linux_isolation(self):
        self.prepare_linux_scale()
        self.fixture.case["isolateNativeText"] = True
        self.fixture.raw["options"]["isolateNativeText"] = True
        native_relative = "runtimes/linux-x64/native/libHarfBuzzSharp.so"
        library = self.fixture.root / native_relative
        library.parent.mkdir(parents=True, exist_ok=True)
        library.write_bytes(b"Validator fixture, never loaded as a native library")
        digest = sha(library)
        self.fixture.assemblies.append({"path": native_relative, "sha256": digest, "bytes": library.stat().st_size})
        self.fixture.raw["nativeModules"] = [{"path": str(library), "sha256": digest}]
        for phase in ("environmentBefore", "environmentAfter"):
            self.fixture.raw[phase]["session"]["nativeTextIsolation"] = {"requested": True, "applied": True, "path": str(library), "symbolProviderPath": str(library), "sha256": digest, "bytes": library.stat().st_size, "flags": 10, "flagNames": "RTLD_NOW|RTLD_LOCAL|RTLD_DEEPBIND", "handle": "0xABCDEF", "error": None, "resolverRegistered": True, "resolverObserved": True, "resolvedLibraryName": "libHarfBuzzSharp", "hostOnly": True, "ordinaryConsumerNativeTextCertified": False}
        self.fixture.raw["nativeTextIsolation"] = deepcopy(self.fixture.raw["environmentAfter"]["session"]["nativeTextIsolation"])

    def test_explicit_linux_isolation_requires_pinned_loaded_library(self):
        self.prepare_linux_isolation()
        self.assertTrue(self.inspect_linux_scale()["passed"])
        self.fixture.raw["nativeModules"] = []
        self.assertFalse(self.inspect_linux_scale()["passed"])

    def test_claimed_isolation_cannot_replace_actual_bootstrap(self):
        self.prepare_linux_isolation()
        self.fixture.raw["environmentBefore"]["session"]["nativeTextIsolation"]["applied"] = False
        self.assertFalse(self.inspect_linux_scale()["passed"])

    def test_wrong_native_binding_flags_are_rejected(self):
        self.prepare_linux_isolation()
        for phase in ("environmentBefore", "environmentAfter"):
            self.fixture.raw[phase]["session"]["nativeTextIsolation"]["flags"] = 2
        self.assertFalse(self.inspect_linux_scale()["passed"])

    def test_linux_global_multiplier_and_actual_scale_must_both_match(self):
        self.prepare_linux_scale()
        self.assertTrue(self.inspect_linux_scale()["passed"])

    def test_linux_scalar_screen_setting_cannot_substitute_global_multiplier(self):
        self.prepare_linux_scale()
        for phase in ("environmentBefore", "environmentAfter"):
            self.fixture.raw[phase]["session"] = {"globalScaleFactor": None, "screenScaleFactors": "1.25"}
        self.assertFalse(self.inspect_linux_scale()["passed"])

    def test_linux_conflicting_screen_override_is_rejected(self):
        self.prepare_linux_scale()
        self.fixture.raw["environmentAfter"]["session"]["screenScaleFactors"] = "rdp-0=2"
        self.assertFalse(self.inspect_linux_scale()["passed"])

    def test_correct_environment_cannot_hide_wrong_initial_actual_scale(self):
        self.prepare_linux_scale()
        self.fixture.raw["environmentBefore"]["window"]["renderScaling"] = 1
        self.assertFalse(self.inspect_linux_scale()["passed"])

    def test_missing_scene_contract_is_not_hidden_by_passed_flag(self):
        self.fixture.raw["suite"]["checks"].pop(self.fixture.contract["requiredCheckKeys"][0])
        self.assert_rejected()

    def test_extra_or_replaced_scene_contract_is_rejected(self):
        self.fixture.raw["suite"]["checks"]["invented-smaller-coverage"] = True
        self.assert_rejected()

    def test_false_scene_assertion_is_not_hidden_by_summary(self):
        self.fixture.raw["suite"]["checks"][self.fixture.contract["requiredCheckKeys"][0]] = False
        self.assert_rejected()

    def test_missing_host_contract_is_rejected(self):
        self.fixture.raw["checks"].pop(HOST_CHECKS[0])
        self.assert_rejected()

    def test_fault_is_not_hidden_by_all_true_checks(self):
        self.fixture.raw["suite"]["faults"] = ["native path failed"]
        self.assert_rejected()

    def test_observed_scale_not_requested_scale_decides(self):
        self.fixture.raw["environmentAfter"]["window"]["renderScaling"] = 1
        self.assert_rejected()

    def test_observed_culture_does_not_follow_only_options(self):
        self.fixture.raw["environmentAfter"]["culture"]["currentCulture"] = "en-US"
        self.assert_rejected()

    def test_derived_client_pixel_size_is_cross_checked(self):
        self.fixture.raw["environmentAfter"]["window"]["physicalClientSize"]["width"] = 100
        self.assert_rejected()

    def test_headless_backend_cannot_substitute_native_window(self):
        self.fixture.raw["environmentAfter"]["window"]["actualBackend"] = "Headless"
        self.assert_rejected()

    def test_consistent_X11_labels_cannot_replace_expected_Windows_backend(self):
        for phase in ("environmentBefore", "environmentAfter"):
            window = self.fixture.raw[phase]["window"]
            window.update(actualBackend="X11", backendType="Avalonia.X11.X11Window", handleDescriptor="XID")
        self.assert_rejected()

    def test_after_only_backend_relabel_does_not_hide_Windows_origin(self):
        self.fixture.raw["environmentAfter"]["window"].update(actualBackend="X11", backendType="Avalonia.X11.X11Window", handleDescriptor="XID")
        self.assert_rejected()

    def test_runtime_OS_relabel_cannot_replace_expected_platform(self):
        for phase in ("environmentBefore", "environmentAfter"):
            self.fixture.raw[phase]["runtime"]["osDescription"] = "Ubuntu 24.04 LTS"
        self.assert_rejected()

    def test_process_architecture_is_bound_to_expected_architecture(self):
        for phase in ("environmentBefore", "environmentAfter"):
            self.fixture.raw[phase]["runtime"]["processArchitecture"] = "ARM64"
        self.assert_rejected()

    def test_runtime_before_after_process_identity_must_match(self):
        self.fixture.raw["environmentAfter"]["runtime"]["processId"] = 5678
        self.assert_rejected()

    def test_same_case_must_retain_its_native_window(self):
        self.fixture.raw["environmentAfter"]["window"]["handle"] = "0xBAD123"
        self.assert_rejected()

    def test_DPI_is_cross_checked_with_actual_user32_observation(self):
        self.fixture.raw["environmentAfter"]["window"]["win32ReportedMetrics"]["dpiForWindow"] = 144
        self.assert_rejected()

    def test_native_user32_pixel_size_cannot_be_replaced_with_DIP(self):
        self.fixture.raw["environmentAfter"]["window"]["win32ReportedMetrics"]["clientRect"]["width"] = 100
        self.assert_rejected()

    def test_unavailable_user32_DPI_is_not_certified(self):
        self.fixture.raw["environmentAfter"]["window"]["win32ReportedMetrics"] = None
        self.assert_rejected()

    def test_renderer_request_does_not_decide_observed_renderer(self):
        self.fixture.raw["environmentAfter"]["rendering"]["actualRenderer"] = "gpu-context"
        self.assert_rejected()

    def test_gpu_request_with_cpu_native_surface_is_rejected(self):
        self.fixture.case["renderer"] = "gpu"
        self.fixture.raw["options"]["renderer"] = "gpu"
        self.assert_rejected()

    def test_mixed_native_surface_is_not_qualified(self):
        self.fixture.raw["environmentAfter"]["rendering"]["actualRenderer"] = "mixed"
        self.fixture.raw["environmentAfter"]["rendering"]["nativeWindowSurfaceObservations"][1]["hasGrContext"] = True
        self.assert_rejected()

    def test_missing_native_epoch_cannot_substitute_offscreen_context(self):
        self.fixture.raw["environmentAfter"]["rendering"]["nativeWindowSurfaceObservations"][1]["epoch"] = 1
        self.assert_rejected()

    def test_offscreen_phase_cannot_substitute_native_before_after(self):
        self.fixture.raw["environmentAfter"]["rendering"]["nativeWindowSurfaceObservations"][1]["phase"] = "export-offscreen"
        self.assert_rejected()

    def test_unknown_native_sample_does_not_disappear(self):
        self.fixture.raw["environmentAfter"]["rendering"]["nativeWindowSurfaceObservations"].append({"epoch": 3, "phase": "scene", "hasGrContext": None, "error": "lease unavailable"})
        self.assert_rejected()

    def test_gpu_api_context_is_not_hardware_gpu_proof(self):
        self.fixture.raw["environmentAfter"]["rendering"]["hardwareGpuProven"] = True
        self.assert_rejected()

    def test_physical_input_cannot_be_self_certified(self):
        self.fixture.raw["qualification"]["physicalInputValidated"] = True
        self.assert_rejected()

    def test_compositor_cannot_be_self_certified_by_raster_export(self):
        self.fixture.raw["qualification"]["compositorPresentValidated"] = True
        self.assert_rejected()

    def test_wrong_actual_product_module_is_rejected(self):
        self.fixture.raw["environmentAfter"]["assemblies"][1]["sha256"] = "B" * 64
        self.assert_rejected()

    def test_duplicate_loaded_product_module_is_rejected(self):
        self.fixture.raw["environmentAfter"]["assemblies"].append(self.fixture.raw["environmentAfter"]["assemblies"][1])
        self.assert_rejected()

    def test_claimed_pixel_dimensions_are_independently_decoded(self):
        self.fixture.raw["suite"]["artifacts"][0]["pixelWidth"] = 9
        self.assert_rejected()

    def test_pixel_hash_is_independently_decoded(self):
        self.fixture.raw["suite"]["artifacts"][0]["decodedBgraSha256"] = "0" * 64
        self.assert_rejected()

    def test_stale_artifact_outside_selected_case_is_rejected(self):
        external = Path(self.temp.name) / "stale.png"
        external.write_bytes((self.fixture.root / "baseline.png").read_bytes())
        self.fixture.raw["suite"]["artifacts"][0]["path"] = str(external)
        self.assert_rejected()

    def test_symbolic_link_escape_is_rejected_when_supported(self):
        target = Path(self.temp.name) / "outside.png"
        target.write_bytes((self.fixture.root / "baseline.png").read_bytes())
        link = self.fixture.root / "linked.png"
        try:
            link.symlink_to(target)
        except OSError:
            self.skipTest("Platform does not permit creating a temporary symbolic link")
        self.fixture.raw["suite"]["artifacts"][0]["path"] = "linked.png"
        self.assert_rejected()

    def test_unlisted_raster_cannot_hide_from_artifact_index(self):
        (self.fixture.root / "unlisted.png").write_bytes((self.fixture.root / "baseline.png").read_bytes())
        self.fixture.raw["suite"]["observations"][0]["oracle"]["pngEqualPairs"][0]["secondPath"] = "unlisted.png"
        self.assert_rejected()

    def test_transient_content_difference_in_equal_pair_is_rejected(self):
        self.fixture.raw["suite"]["observations"][0]["oracle"]["pngEqualPairs"][0]["secondPath"] = "hidden.png"
        self.assert_rejected()

    def test_selection_difference_cannot_be_empty(self):
        self.fixture.raw["suite"]["observations"][0]["oracle"]["pngDifferentPairs"][0]["secondPath"] = "restored.png"
        self.assert_rejected()

    def test_glyph_hidden_pixels_must_really_change(self):
        self.fixture.raw["suite"]["observations"][0]["oracle"]["glyphProofs"][0]["hiddenPath"] = "restored.png"
        self.assert_rejected()

    def test_glyph_restoration_requires_whole_frame_restoration(self):
        self.fixture.raw["suite"]["observations"][0]["oracle"]["glyphProofs"][0]["restoredPath"] = "hidden.png"
        self.assert_rejected()

    def test_glyph_roi_cannot_be_clipped_to_a_favorable_subset(self):
        self.fixture.raw["suite"]["observations"][0]["oracle"]["glyphProofs"][0]["pixelRegion"]["x"] = -1
        self.assert_rejected()

    def test_crc_corruption_is_not_hidden_by_hash_restatement(self):
        path = self.fixture.root / "baseline.png"
        data = bytearray(path.read_bytes())
        data[-1] ^= 1
        path.write_bytes(data)
        self.fixture.raw["suite"]["artifacts"][0]["sha256"] = sha(path)
        self.assert_rejected()

    def test_empty_expected_key_contract_is_not_a_pass(self):
        self.fixture.contract["requiredCheckKeys"] = []
        self.assert_rejected()


class InputManifestTests(unittest.TestCase):
    def test_reordered_equal_manifests_match(self):
        rows = [{"path": "a.cs", "sha256": "A" * 64, "bytes": 3}, {"path": "b.cs", "sha256": "B" * 64, "bytes": 4}]
        self.assertTrue(same_manifest(rows, list(reversed(rows))))

    def test_source_hash_change_is_not_hidden_by_same_count(self):
        before = [{"path": "a.cs", "sha256": "A" * 64, "bytes": 3}]
        after = [{"path": "a.cs", "sha256": "B" * 64, "bytes": 3}]
        self.assertFalse(same_manifest(before, after))

    def test_new_source_cannot_hide_behind_existing_set(self):
        before = [{"path": "a.cs", "sha256": "A" * 64, "bytes": 3}]
        after = before + [{"path": "new.cs", "sha256": "B" * 64, "bytes": 3}]
        self.assertFalse(same_manifest(before, after))

    def test_duplicate_source_path_cannot_mask_hash(self):
        rows = [{"path": "a.cs", "sha256": "A" * 64, "bytes": 3}]
        with self.assertRaises(ValueError):
            same_manifest(rows, rows + rows)

    def test_json_duplicate_property_is_rejected(self):
        with temporary_workspace() as temp:
            path = Path(temp) / "duplicate.json"
            path.write_text('{"passed":false,"passed":true}', encoding="utf-8")
            with self.assertRaises(ValueError):
                load(path)


def deterministic_fixture():
    rows = []
    for name, size in (("desktop-main", [1280, 800]), ("desktop-small", [640, 360]), ("desktop-restored", [1280, 800])):
        rows.append({"kind": "cartesian-geometry", "scene": name, "actualClientDips": size,
                     "plotDips": [10, 20, 100, 100], "projectedPrimaryPlotDips": [50, 40],
                     "actualHit": {"id": "B", "sourceIndex": 1, "raw": [2, 60]},
                     "originalSource": [{"id": "A", "x": 1, "y": 20}, {"id": "B", "x": 2, "y": 60}, {"id": "C", "x": 3, "y": 80}]})
    rows.append({"kind": "culture-and-UTC-dates", "culture": "zh-CN", "originalDomainMilliseconds": [1709251200000, 1709337600000, 1709424000000],
                 "labels": ["2024-03-01 00:00:00Z", "2024-03-02 00:00:00Z", "2024-03-03 00:00:00Z"],
                 "dateHit": {"id": "D1", "sourceIndex": 1, "raw": [1709337600000, 62.5]}, "numericLabel": "62.5", "decimalSeparator": ".", "flowDirection": "LeftToRight"})
    rows.append({"kind": "professional-native-frames", "funnelMarks": [{"id": key, "sourceIndex": index, "value": value} for index, (key, value) in enumerate(zip("abcd", (20, 50, 30, 10)))],
                 "mosaicMarks": [{"members": [{"id": "a", "sourceIndex": 0}, {"id": "d", "sourceIndex": 3}]}, {"members": [{"id": "b", "sourceIndex": 1}]}, {"members": [{"id": "c", "sourceIndex": 2}]}],
                 "funnelHit": {"id": "b", "sourceIndex": 1, "raw": [1, 50]}, "mosaicHitMembers": ["a", "d"]})
    return rows


class DeterministicSceneEvidenceTests(unittest.TestCase):
    def test_fixed_fixture_observations_pass_without_producer_pass_flags(self):
        checks = deterministic_scene_checks(deterministic_fixture())
        self.assertTrue(checks and all(checks.values()), checks)

    def test_wrong_projected_coordinate_is_independently_rejected(self):
        rows = deterministic_fixture(); rows[0]["projectedPrimaryPlotDips"][1] = 60
        self.assertFalse(all(deterministic_scene_checks(rows).values()))

    def test_wrong_original_source_identity_is_independently_rejected(self):
        rows = deterministic_fixture(); rows[1]["actualHit"]["sourceIndex"] = 2
        self.assertFalse(all(deterministic_scene_checks(rows).values()))

    def test_requested_size_without_actual_resize_is_rejected(self):
        rows = deterministic_fixture(); rows[1]["actualClientDips"] = [1280, 800]
        self.assertFalse(all(deterministic_scene_checks(rows).values()))

    def test_failed_restore_is_rejected(self):
        rows = deterministic_fixture(); rows[2]["actualClientDips"] = [640, 360]
        self.assertFalse(all(deterministic_scene_checks(rows).values()))

    def test_local_calendar_label_cannot_replace_fixed_UTC_label(self):
        rows = deterministic_fixture(); rows[3]["labels"][0] = "1445-08-20"
        self.assertFalse(all(deterministic_scene_checks(rows).values()))

    def test_date_hit_retains_original_fractional_value(self):
        rows = deterministic_fixture(); rows[3]["dateHit"]["raw"][1] = 60
        self.assertFalse(all(deterministic_scene_checks(rows).values()))

    def test_partial_aggregate_cannot_replace_complete_original_members(self):
        rows = deterministic_fixture(); rows[4]["mosaicHitMembers"] = ["a"]
        self.assertFalse(all(deterministic_scene_checks(rows).values()))

    def test_aggregate_members_cannot_copy_a_different_original_index(self):
        rows = deterministic_fixture(); rows[4]["mosaicMarks"][0]["members"][1]["sourceIndex"] = 0
        self.assertFalse(all(deterministic_scene_checks(rows).values()))


class ProcessEvidenceTests(unittest.TestCase):
    def setUp(self):
        self.temp = temporary_workspace()
        self.directory = Path(self.temp.name)
        self.fixture = EvidenceFixture(self.directory / "cases/software-fixture")
        self.fixture.case["processScreenScale"] = None
        self.fixture.save()
        self.receipt = {"id": "case-software-fixture", "passed": True, "exitCode": 0, "timedOut": False, "processId": 1234}
        self.project = "benchmarks/Charts.PlatformAcceptance/Charts.PlatformAcceptance.csproj"
        self.build_receipts = [{"id": "restore", "passed": True, "exitCode": 0, "timedOut": False, "arguments": ["restore", self.project, "--locked-mode"]}, {"id": "build", "passed": True, "exitCode": 0, "timedOut": False, "arguments": ["build", self.project, "--no-incremental"]}]
        self.run = {"schema": "charts-platform-acceptance-run/v1", "platform": "windows", "osDescription": "Microsoft Windows 10.0.26200", "architecture": "X64", "nativeProcessesPassed": True, "error": None, "planOnly": False, "sourceUnchanged": True, "assembliesUnchanged": True, "processExecutions": self.build_receipts + [self.receipt], "sourceAssociation": {"method": "same-run-locked-restore-and-nonincremental-source-build", "hostProject": self.project, "binaryOnlyQualificationAllowed": False}}
        self.source = [{"path": "fixture.cs", "sha256": "A" * 64, "bytes": 10}]
        self.write("plan.json", {"schema": "charts-platform-acceptance-plan/v1", "cases": [self.fixture.case]})
        for name in ("source-before.json", "source-after.json"):
            self.write(name, self.source)
        for name in ("assemblies-before.json", "assemblies-after.json"):
            self.write(name, self.fixture.assemblies)

    def write(self, name, value):
        target = self.directory / name
        target.parent.mkdir(parents=True, exist_ok=True)
        target.write_text(json.dumps(value), encoding="utf-8")

    def inspect(self):
        self.write("run.json", self.run)
        self.write("processes/case-software-fixture/process.json", self.receipt)
        for receipt in self.build_receipts:
            self.write("processes/" + receipt["id"] + "/process.json", receipt)
        return inspect_run(self.directory, self.fixture.contract, expected_platform="windows", expected_architecture="X64")

    def tearDown(self):
        self.temp.cleanup()

    def test_consistent_process_records_pass(self):
        result = self.inspect()
        self.assertTrue(result["passed"], result)

    def test_duplicate_process_id_cannot_hide_a_failure(self):
        failed = deepcopy(self.receipt); failed["passed"] = False; failed["exitCode"] = 1
        self.run["processExecutions"].insert(0, failed)
        self.assertFalse(self.inspect()["passed"])

    def test_failed_native_process_is_not_hidden_by_good_pixels(self):
        self.receipt["passed"] = False; self.receipt["exitCode"] = 1
        self.assertFalse(self.inspect()["passed"])

    def test_false_boolean_does_not_substitute_integer_process_exit_code(self):
        self.receipt["exitCode"] = False
        self.assertFalse(self.inspect()["passed"])

    def test_source_change_is_not_hidden_by_declared_true_flag(self):
        self.write("source-after.json", [{"path": "fixture.cs", "sha256": "B" * 64, "bytes": 10}])
        self.assertFalse(self.inspect()["passed"])

    def test_missing_native_case_receipt_is_not_hidden_by_positive_case_count(self):
        self.run["processExecutions"] = []
        self.assertFalse(self.inspect()["passed"])

    def test_old_binary_cannot_substitute_same_run_source_build(self):
        self.run["sourceAssociation"]["method"] = "binary-only"
        self.run["processExecutions"] = [self.receipt]
        self.assertFalse(self.inspect()["passed"])

    def test_missing_locked_restore_cannot_claim_source_association(self):
        self.build_receipts[0]["arguments"].remove("--locked-mode")
        self.assertFalse(self.inspect()["passed"])

    def test_incremental_build_does_not_prove_new_source_compilation(self):
        self.build_receipts[1]["arguments"].remove("--no-incremental")
        self.assertFalse(self.inspect()["passed"])

    def test_parent_platform_is_bound_to_independent_expected_platform(self):
        self.run["platform"] = "linux"
        self.run["osDescription"] = "Ubuntu 24.04 LTS"
        self.assertFalse(self.inspect()["passed"])

    def test_json_nonfinite_value_is_rejected(self):
        with temporary_workspace() as temp:
            path = Path(temp) / "nan.json"
            path.write_text('{"scale":NaN}', encoding="utf-8")
            with self.assertRaises(ValueError):
                load(path)


class LogicalDisplayEvidenceTests(unittest.TestCase):
    def setUp(self):
        self.environment = {"screens": [{"bounds": {"x": 0, "y": 0, "width": 1920, "height": 1080}, "workingArea": {"x": 0, "y": 0, "width": 1920, "height": 1032}, "scaling": 1}, {"bounds": {"x": 1920, "y": 0, "width": 1920, "height": 1080}, "workingArea": {"x": 1920, "y": 0, "width": 1920, "height": 1032}, "scaling": 1}]}
        viewport = {"xMinimum": .5, "xMaximum": 3.5, "yMinimum": 10, "yMaximum": 90}
        self.target = {"kind": "logical-display-migration-target", "targetIndex": 1, "status": "executed", "checkPrefix": "logical-display-1", "expectedBounds": [1920, 0, 1920, 1080], "observedBounds": [1920, 0, 1920, 1080], "reportedScreenScaling": 1, "actualRenderScaling": 1, "actualClientDips": [640, 360], "actualHit": {"id": "B", "sourceIndex": 1, "raw": [2, 60]}, "selectedId": "B", "viewportBefore": viewport, "viewportAfter": deepcopy(viewport), "physicalCrossMonitorCertified": False, "mixedDpiCertified": False}
        self.capability = {"kind": "logical-display-migration-capability", "status": "executed-logical-topology-only", "logicalScreenCount": 2, "executedTargets": 1, "originalScreenBounds": [0, 0, 1920, 1080], "originalPositionPixels": [50, 50], "restoredPositionPixels": [50, 50], "physicalCrossMonitorCertified": False, "mixedDpiCertified": False}
        for prefix in ("original", "restored"):
            self.capability.update({prefix + "ClientOriginScreenPixels": [50, 50], prefix + "ClientCenterScreenPixels": [370, 230], prefix + "ClientDips": [640, 360], prefix + "RenderScaling": 1, prefix + "ScreenBounds": [0, 0, 1920, 1080]})
        self.capability["placementTrace"] = [dict(sequence=index, kind=kind, actualPositionPixels=[50, 50], actualPointToScreenOrigin=[50, 50], actualClientCenterScreenPixels=[370, 230], actualClientDips=[640, 360], actualRenderScaling=1, actualScreenBounds=[0, 0, 1920, 1080]) for index, kind in enumerate(("original-placement-observed", "restore-bounded-feedback-finished"), 1)]

    def inspect(self):
        return logical_display_protocol([self.target, self.capability], self.environment)

    def test_executed_logical_target_derives_exact_extra_coverage(self):
        result = self.inspect()
        self.assertTrue(all(result["checks"].values()), result)
        self.assertEqual(7, len(result["extraKeys"]))
        self.assertEqual(1, len(result["extraArtifacts"]))
        self.assertFalse(result["physicalCrossMonitorCertified"])

    def test_missing_available_target_does_not_become_a_pass(self):
        result = logical_display_protocol([self.capability], self.environment)
        self.assertFalse(all(result["checks"].values()))

    def test_wrong_observed_screen_is_rejected(self):
        self.target["observedBounds"] = [0, 0, 1920, 1080]
        self.assertFalse(all(self.inspect()["checks"].values()))

    def test_explicit_origin_is_used_when_resize_changed_native_position(self):
        self.capability.update(originalPositionPixels=[-22, -22], restoredPositionPixels=[-22, -22])
        for row in self.capability["placementTrace"]:
            row["actualPositionPixels"] = [-22, -22]
        before = {"window": {"positionPixels": [38, 59], "currentScreen": {"bounds": self.environment["screens"][0]["bounds"]}}}
        result = logical_display_protocol([self.target, self.capability], self.environment, before)
        self.assertTrue(all(result["checks"].values()), result)
        self.assertEqual("capability-record", result["originalScreenSource"])

    def test_stale_before_window_cannot_supply_missing_migration_origin(self):
        self.capability.pop("originalScreenBounds")
        self.capability.update(originalPositionPixels=[-22, -22], restoredPositionPixels=[-22, -22])
        before = {"window": {"positionPixels": [38, 59], "currentScreen": {"bounds": self.environment["screens"][0]["bounds"]}}}
        result = logical_display_protocol([self.target, self.capability], self.environment, before)
        self.assertFalse(all(result["checks"].values()), result)
        self.assertEqual("missing-original-screen", result["status"])

    def test_claimed_origin_outside_actual_topology_is_rejected(self):
        self.capability["originalScreenBounds"] = [-9999, 0, 1920, 1080]
        self.assertFalse(all(self.inspect()["checks"].values()))

    def test_restored_position_must_match_exact_native_origin(self):
        self.capability["restoredPositionPixels"] = [51, 50]
        self.assertFalse(all(self.inspect()["checks"].values()))

    def test_wrong_native_origin_center_is_rejected(self):
        self.capability["originalClientCenterScreenPixels"] = [9999, 230]
        self.assertFalse(all(self.inspect()["checks"].values()))

    def test_restored_client_geometry_must_match_original(self):
        self.capability["restoredClientOriginScreenPixels"] = [49, 50]
        self.assertFalse(all(self.inspect()["checks"].values()))

    def test_missing_native_placement_trace_is_rejected(self):
        self.capability.pop("placementTrace")
        self.assertFalse(all(self.inspect()["checks"].values()))

    def test_trace_cannot_disagree_with_the_claimed_restoration(self):
        self.capability["placementTrace"][-1]["actualPositionPixels"] = [1920, 0]
        self.assertFalse(all(self.inspect()["checks"].values()))

    def test_cross_screen_original_identity_must_remain_original(self):
        self.target["actualHit"]["sourceIndex"] = 2
        self.assertFalse(all(self.inspect()["checks"].values()))

    def test_unchanged_but_wrong_viewport_does_not_pass(self):
        self.target["viewportBefore"]["xMinimum"] = 0
        self.target["viewportAfter"]["xMinimum"] = 0
        self.assertFalse(all(self.inspect()["checks"].values()))

    def test_blocked_target_is_explicit_and_not_counted_as_executed(self):
        self.environment["screens"][1]["workingArea"]["width"] = 400
        self.target["status"] = "blocked-target-working-area-too-small"
        self.capability.update(executedTargets=0, status="blocked-no-target-workarea")
        result = self.inspect()
        self.assertTrue(all(result["checks"].values()), result)
        self.assertEqual(0, result["executedTargets"])
        self.assertEqual([1], result["blockedTargets"])
        self.assertEqual([], result["extraKeys"])

    def test_usable_workarea_cannot_be_claimed_blocked(self):
        self.target["status"] = "blocked-target-working-area-too-small"
        self.capability.update(executedTargets=0, status="blocked-no-target-workarea")
        self.assertFalse(all(self.inspect()["checks"].values()))

    def test_logical_migration_cannot_certify_physical_mixed_DPI(self):
        self.target["mixedDpiCertified"] = True
        self.assertFalse(all(self.inspect()["checks"].values()))


class RunnerParameterBoundaryTests(unittest.TestCase):
    def test_removed_old_binary_modes_cannot_be_used_even_with_PlanOnly(self):
        shell = shutil.which("pwsh")
        if shell is None:
            self.skipTest("PowerShell 7 unavailable; parameter binding has not been executed")
        with temporary_workspace() as temporary:
            for option in ("-SkipBuild", "-HostDirectory"):
                with self.subTest(option=option):
                    target = Path(temporary) / option.removeprefix("-")
                    args = [shell, "-NoLogo", "-NoProfile", "-File", str(SCRIPTS / "Invoke-ChartsPlatformAcceptance.ps1"), "-OutputDirectory", str(target), "-PlanOnly", option]
                    if option == "-HostDirectory":
                        args.append(str(Path(temporary) / "old-binary"))
                    result = subprocess.run(args, cwd=SCRIPTS.parent, capture_output=True, text=True, errors="replace", timeout=30)
                    self.assertNotEqual(0, result.returncode)
                    self.assertFalse(target.exists(), result.stdout + result.stderr)


class HiddenManifestInputTests(unittest.TestCase):
    def setUp(self):
        self.shell = shutil.which("pwsh")
        if self.shell is None:
            self.skipTest("PowerShell 7 unavailable; source-manifest function has not been executed")
        self.temp = temporary_workspace()
        self.root = Path(self.temp.name)

    def tearDown(self):
        if hasattr(self, "temp"):
            self.temp.cleanup()

    @staticmethod
    def make_hidden(path):
        if os.name != "nt":
            return  # Leading-dot names are hidden to Linux PowerShell.
        import ctypes
        kernel = ctypes.WinDLL("kernel32", use_last_error=True)
        kernel.GetFileAttributesW.argtypes = [ctypes.c_wchar_p]
        kernel.GetFileAttributesW.restype = ctypes.c_uint32
        kernel.SetFileAttributesW.argtypes = [ctypes.c_wchar_p, ctypes.c_uint32]
        kernel.SetFileAttributesW.restype = ctypes.c_int
        attributes = kernel.GetFileAttributesW(str(path))
        if attributes == 0xFFFFFFFF or not kernel.SetFileAttributesW(str(path), attributes | 2):
            raise OSError(ctypes.get_last_error(), "Cannot mark owned fixture input hidden")

    def write(self, relative, content, hidden=False):
        path = self.root / relative
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_bytes(content)
        if hidden:
            self.make_hidden(path)
        return path

    def invoke_function(self, name, *, sort_only=False):
        def quote(value):
            return "'" + str(value).replace("'", "''") + "'"
        command = r"""
$ErrorActionPreference='Stop'
$fixtureTokens=$null;$fixtureParseErrors=$null
$fixtureAst=[Management.Automation.Language.Parser]::ParseFile(@SCRIPT@,[ref]$fixtureTokens,[ref]$fixtureParseErrors)
if($fixtureParseErrors.Count){throw 'Runner parse failed'}
$fixtureFunction=$fixtureAst.Find({param($n) $n -is [Management.Automation.Language.FunctionDefinitionAst] -and $n.Name -eq @FUNCTION@},$true)
if($null -eq $fixtureFunction){throw 'Required manifest function missing'}
@BODY@
"""
        if sort_only:
            body = r"""
$sortNode=$fixtureFunction.Body.Find({param($n) $n -is [Management.Automation.Language.CommandAst] -and $n.GetCommandName() -eq 'Sort-Object'},$true)
if($null -eq $sortNode){throw 'Source manifest sort missing'}
$sorted=@(Invoke-Expression ("@('Distinct.cs','distinct.cs') | " + $sortNode.Extent.Text))
ConvertTo-Json -InputObject $sorted -Depth 10
"""
        else:
            body = r"""
Invoke-Expression $fixtureFunction.Extent.Text
$acceptanceRepository=@ROOT@
$result=@(@INVOKE@)
ConvertTo-Json -InputObject $result -Depth 10
"""
            invocation = "Get-AcceptanceSources" if name == "Get-AcceptanceSources" else "Get-AcceptanceAssemblies -Directory " + quote(self.root)
            body = body.replace("@ROOT@", quote(self.root)).replace("@INVOKE@", invocation)
        command = command.replace("@SCRIPT@", quote(SCRIPTS / "Invoke-ChartsPlatformAcceptance.ps1")).replace("@FUNCTION@", quote(name)).replace("@BODY@", body)
        process = subprocess.run([self.shell, "-NoLogo", "-NoProfile", "-Command", command], cwd=SCRIPTS.parent, capture_output=True, text=True, encoding="utf-8", errors="replace", timeout=30)
        self.assertEqual(0, process.returncode, process.stdout + process.stderr)
        return json.loads(process.stdout.lstrip("\ufeff"))

    def test_explicit_hidden_editorconfig_is_hashed_not_lost_after_TestPath(self):
        path = self.write(".editorconfig", b"root = true\n", hidden=True)
        result = self.invoke_function("Get-AcceptanceSources")
        self.assertEqual([{"path": ".editorconfig", "sha256": sha(path), "bytes": path.stat().st_size}], result)

    def test_recursive_hidden_sources_and_directories_are_in_complete_manifest(self):
        prefix = "src/NT.AvaloniaControl.Charts/"
        files = [self.write(prefix + "Visible.cs", b"visible\n"), self.write(prefix + ".hidden.cs", b"hidden\n", hidden=True), self.write(prefix + ".hidden-directory/Included.cs", b"hidden directory\n")]
        self.make_hidden(self.root / prefix / ".hidden-directory")
        self.write(prefix + "bin/.generated.cs", b"excluded bin\n", hidden=True)
        self.write(prefix + "obj/.generated.cs", b"excluded obj\n", hidden=True)
        self.write(prefix + ".notes.md", b"excluded documentation\n", hidden=True)
        result = self.invoke_function("Get-AcceptanceSources")
        actual = {row["path"]: (row["sha256"], row["bytes"]) for row in result}
        expected = {path.relative_to(self.root).as_posix(): (sha(path), path.stat().st_size) for path in files}
        self.assertEqual(expected, actual)

    def test_hidden_output_modules_and_runtimeconfig_are_pinned(self):
        files = [self.write("Visible.dll", b"visible module\n"), self.write(".hidden.dll", b"hidden module\n", hidden=True), self.write(".hidden.runtimeconfig.json", b"{}\n", hidden=True), self.write("libHarfBuzzSharp.so", b"pinned native text asset\n")]
        self.write(".unrelated.log", b"not a module\n", hidden=True)
        files.append(self.write("runtimes/linux-x64/native/libHarfBuzzSharp.so", b"nested native text asset\n"))
        actual = {row["path"]: (row["sha256"], row["bytes"]) for row in self.invoke_function("Get-AcceptanceAssemblies")}
        self.assertEqual({path.relative_to(self.root).as_posix(): (sha(path), path.stat().st_size) for path in files}, actual)

    def test_source_sort_does_not_merge_case_distinct_Linux_paths(self):
        result = self.invoke_function("Get-AcceptanceSources", sort_only=True)
        self.assertEqual({"Distinct.cs", "distinct.cs"}, set(result))
        self.assertEqual(2, len(result))


class GlyphDirectionPixelTests(unittest.TestCase):
    def setUp(self):
        self.width, self.height = 48, 24
        baseline = bytearray(b"\xff\xff\xff\xff" * (self.width * self.height))
        hidden = bytearray(baseline)
        # Deliberately asymmetric uppercase F drawn from a fixture mask, not
        # product code or a FlowDirection/BypassFlowDirectionPolicies assertion.
        def draw(canvas, origin):
            for y in range(3, 21):
                for x in range(3, 16):
                    if x < 6 or y < 6 or 10 <= y < 13 and x < 13:
                        at = (y * self.width + x + origin) * 4
                        canvas[at:at + 3] = b"\x00\x00\x00"
        draw(baseline, 0); draw(baseline, 24); draw(hidden, 24)
        self.baseline = (self.width, self.height, bytes(baseline))
        self.hidden = (self.width, self.height, bytes(hidden))
        self.region = {"x": 0, "y": 0, "width": 24, "height": 24}
        self.reference = {"x": 24, "y": 0, "width": 24, "height": 24}

    def test_upright_actual_ink_and_native_reference_pass(self):
        result = glyph_orientation_evidence(self.baseline, self.hidden, self.baseline, self.region, self.reference)
        self.assertTrue(result["passed"], result)
        self.assertEqual("left", result["chartGlyph"]["lowerStem"])

    def test_horizontal_reflection_is_rejected_with_same_restore_and_pixels(self):
        reflected = bytearray(self.baseline[2])
        for y in range(self.height):
            for x in range(24):
                source = (y * self.width + 23 - x) * 4
                target = (y * self.width + x) * 4
                reflected[target:target + 4] = self.baseline[2][source:source + 4]
        changed = (self.width, self.height, bytes(reflected))
        result = glyph_orientation_evidence(changed, self.hidden, changed, self.region, self.reference)
        self.assertFalse(result["passed"], result)
        self.assertEqual("right", result["chartGlyph"]["lowerStem"])
        self.assertTrue(result["wholeFrameRestored"])

    def test_direction_cannot_pass_when_ink_was_not_rendered(self):
        result = glyph_orientation_evidence(self.hidden, self.hidden, self.hidden, self.region, self.reference)
        self.assertFalse(result["passed"])

    def test_direction_cannot_use_wrong_native_reference(self):
        result = glyph_orientation_evidence(self.baseline, self.hidden, self.baseline, self.region, self.region)
        self.assertFalse(result["passed"])
        result = glyph_orientation_evidence(self.baseline, self.hidden, self.baseline, self.reference, self.region)
        self.assertFalse(result["passed"])

    def test_changed_restored_frame_cannot_hide_direction_failure(self):
        result = glyph_orientation_evidence(self.baseline, self.hidden, self.hidden, self.region, self.reference)
        self.assertFalse(result["passed"])

    def test_hiding_only_text_does_not_change_surrounding_parent_backdrop(self):
        self.assertEqual(0, outside_region_changes(self.baseline, self.hidden, self.region))

    def test_parent_background_change_cannot_substitute_actual_text_ink(self):
        pixels = bytearray(self.hidden[2])
        pixels[(23 * self.width + 46) * 4] = 0
        altered = (self.width, self.height, bytes(pixels))
        self.assertEqual(1, outside_region_changes(self.baseline, altered, self.region))


if __name__ == "__main__":
    unittest.main()
