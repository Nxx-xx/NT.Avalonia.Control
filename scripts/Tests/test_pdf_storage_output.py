"""Unprivileged contracts for the isolated Linux storage probe's output setup."""
import sys
import tempfile
import unittest
from pathlib import Path
from unittest.mock import patch

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / 'pdfviewer'))
from storage_evidence import prepare_output


class StorageOutputTests(unittest.TestCase):
    def test_missing_parents_exist_before_space_check(self):
        with tempfile.TemporaryDirectory() as tmp:
            output = Path(tmp) / 'artifacts' / 'storage' / 'run'
            def check(parent):
                self.assertEqual(parent, output.parent)
                self.assertTrue(parent.is_dir())
                self.assertFalse(output.exists())
            with patch('storage_evidence.require_space', side_effect=check) as check_space:
                self.assertEqual(prepare_output(output), output)
                check_space.assert_called_once()
            self.assertTrue(output.is_dir())

    def test_existing_output_preserved(self):
        with tempfile.TemporaryDirectory() as tmp:
            output = Path(tmp) / 'run'
            output.mkdir()
            marker = output / 'evidence'
            marker.write_text('keep')
            with patch('storage_evidence.require_space') as check_space:
                with self.assertRaises(FileExistsError):
                    prepare_output(output)
                check_space.assert_not_called()
            self.assertEqual(marker.read_text(), 'keep')

    def test_low_space_does_not_create_output(self):
        with tempfile.TemporaryDirectory() as tmp:
            output = Path(tmp) / 'missing' / 'run'
            with patch('storage_evidence.require_space', side_effect=OSError('low space')):
                with self.assertRaisesRegex(OSError, 'low space'):
                    prepare_output(output)
            self.assertFalse(output.exists())

    def test_concurrent_creation_not_reused(self):
        with tempfile.TemporaryDirectory() as tmp:
            output = Path(tmp) / 'run'
            with patch('storage_evidence.require_space', side_effect=lambda parent: output.mkdir()):
                with self.assertRaises(FileExistsError):
                    prepare_output(output)


if __name__ == '__main__':
    unittest.main()
