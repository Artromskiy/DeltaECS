"""Regression checks for isolation, immutable first evidence and evaluator failures."""

import hashlib
import json
from pathlib import Path
import shutil
import subprocess
import sys
import tempfile
import unittest


SUITE = Path(__file__).resolve().parent


class HarnessTests(unittest.TestCase):
    def setUp(self):
        self.scratch = tempfile.TemporaryDirectory()
        self.addCleanup(self.scratch.cleanup)
        self.root = Path(self.scratch.name)
        self.workspace = self.root / "sessions"
        self.session = self.workspace / "a"
        self.task = self.session / "tasks" / "01-test"
        self.task.mkdir(parents=True)
        (self.session / "SESSION.md").write_text("Public instructions\n")
        (self.task / "Consumer.csproj").write_text("<Project />\n")
        (self.task / "Solution.cs").write_text("starter\n")
        inputs = {str(p.relative_to(self.workspace)): hashlib.sha256(p.read_bytes()).hexdigest()
                  for p in self.session.rglob("*") if p.is_file()}
        (self.workspace / "manifest.json").write_text(json.dumps({
            "assignments": {"a": ["01-test"]}, "snapshot_sha256": inputs,
        }))
        self.toolchain = self.root / "toolchain"
        self.toolchain.mkdir()

    def sandbox(self, code):
        return subprocess.run([
            sys.executable, str(SUITE / "sandbox.py"),
            "--workspace", str(self.workspace), "--session", "a",
            "--audit", str(self.root / "audit"), "--dotnet-root", str(self.toolchain),
            "--", "python3", "-c", code,
        ], capture_output=True, text=True, timeout=30)

    @unittest.skipUnless(shutil.which("bwrap"), "bubblewrap is not installed")
    def test_source_sibling_network_and_readonly_inputs(self):
        secret = self.root / "implementation-secret.txt"
        secret.write_text("must never be visible")
        result = self.sandbox(f"""
import errno, pathlib, socket
assert not pathlib.Path({str(secret)!r}).exists()
assert not pathlib.Path('/workspace').exists()
assert not pathlib.Path('/proc/1/root/workspace').exists()
assert not pathlib.Path('/run/docker.sock').exists()
assert not pathlib.Path('/work/.git').exists()
try:
    pathlib.Path('/work/SESSION.md').write_text('tamper')
except OSError as error:
    assert error.errno in (errno.EROFS, errno.EACCES)
else:
    raise AssertionError('supplied input was writable')
try:
    with socket.socket(socket.AF_INET, socket.SOCK_DGRAM) as connection:
        connection.connect(('1.1.1.1', 53))
except OSError:
    pass
else:
    raise AssertionError('network route escaped the namespace')
print('ISOLATION PASS')
""")
        self.assertEqual(result.returncode, 0, result.stdout + result.stderr)
        self.assertIn("ISOLATION PASS", result.stdout)
        self.assertEqual((self.session / "SESSION.md").read_text(), "Public instructions\n")

    @unittest.skipUnless(shutil.which("bwrap"), "bubblewrap is not installed")
    def test_external_first_answer_cannot_be_replaced_from_session(self):
        for value in ["first answer", "second answer"]:
            result = self.sandbox("from pathlib import Path; "
                                  f"Path('tasks/01-test/Solution.cs').write_text({value!r})")
            self.assertEqual(result.returncode, 0, result.stderr)
        first = self.root / "audit/a/first/tasks/01-test/Solution.cs"
        self.assertEqual(first.read_text(), "first answer")
        self.assertEqual((self.task / "Solution.cs").read_text(), "second answer")

    def evaluate(self, dotnet="/usr/bin/false"):
        return subprocess.run([
            sys.executable, str(SUITE / "evaluate.py"),
            "--workspace", str(self.workspace), "--results", str(self.root / "results"),
            "--dotnet", dotnet,
        ], capture_output=True, text=True, timeout=30)

    def test_failed_consumer_is_a_failed_evaluation(self):
        result = self.evaluate()
        self.assertEqual(result.returncode, 1, result.stdout + result.stderr)
        checks = json.loads((self.root / "results/correctness.json").read_text())
        self.assertEqual(checks[0]["exit_code"], 1)

    def test_unexpected_compiler_input_is_rejected(self):
        (self.task / "Override.cs").write_text("unexpected compiler input")
        result = self.evaluate()
        self.assertNotEqual(result.returncode, 0)
        integrity = json.loads((self.root / "results/input-integrity.json").read_text())
        self.assertTrue(any("Override.cs" in item for item in integrity["modified_inputs"]))

    def test_first_failure_does_not_replace_a_passing_final_answer(self):
        (self.task / "Solution.cs").write_text("passing")
        (self.task / "FIRST-Solution.cs.txt").write_text("failing")
        compiler = self.root / "consumer-check"
        compiler.write_text("#!/usr/bin/python3\nfrom pathlib import Path\n"
                            "raise SystemExit(0 if Path('Solution.cs').read_text() == 'passing' else 1)\n")
        compiler.chmod(0o755)
        result = self.evaluate(str(compiler))
        self.assertEqual(result.returncode, 0, result.stdout + result.stderr)
        checks = json.loads((self.root / "results/correctness.json").read_text())
        self.assertEqual(checks[0]["first_check_exit_code"], 1)
        self.assertEqual(checks[0]["exit_code"], 0)
        self.assertEqual((self.task / "Solution.cs").read_text(), "passing")

    def test_modified_supplied_input_is_rejected(self):
        (self.session / "SESSION.md").write_text("tampered")
        result = self.evaluate()
        self.assertNotEqual(result.returncode, 0)
        integrity = json.loads((self.root / "results/input-integrity.json").read_text())
        self.assertIn("a/SESSION.md", integrity["modified_inputs"])


if __name__ == "__main__":
    unittest.main()
