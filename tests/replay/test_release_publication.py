"""Reject mismatched sources, unqualified runs and conflicting public history."""
import importlib.util
import io
import json
from pathlib import Path
import tarfile
import tempfile
import unittest
from unittest.mock import patch
import zipfile

SPEC = importlib.util.spec_from_file_location('publish_release', Path(__file__).resolve().parents[2]/'tools/publish_release.py')
release = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(release)
SHA = 'a'*40


class ReleaseAdmissionTests(unittest.TestCase):
    def setUp(self):
        self.run = dict(name='Windows CI', head_sha=SHA, head_branch='main', event='push',
            status='completed', conclusion='success', head_repository={'full_name':release.REPOSITORY})
        self.checks = {'check_runs':[{'name':name,'status':'completed','conclusion':'success'} for name in (
            'Windows build and tests','Windows validation','Linux desktop tests and coverage','Linux companion package',
            'Windows native tray probe','SonarCloud Code Analysis','Analyze (csharp)',
            'Analyze (python)','Analyze (javascript-typescript)')]}

    def test_qualified_exact_main_run(self):
        with patch.object(release,'api',side_effect=[self.run,self.checks]):
            release.qualified_run('123',SHA)

    def test_rejects_pr_old_commit_failure_and_foreign_repository(self):
        for key,value in [('event','pull_request'),('head_sha','b'*40),('head_branch','feature'),
                          ('conclusion','failure'),('head_repository',{'full_name':'other/repo'})]:
            with self.subTest(key=key), patch.object(release,'api',return_value=self.run|{key:value}):
                with self.assertRaises(ValueError): release.qualified_run('123',SHA)

    def test_security_and_package_checks_cannot_be_skipped(self):
        for item in self.checks['check_runs']:
            checks = {'check_runs':[entry for entry in self.checks['check_runs'] if entry is not item]}
            with self.subTest(name=item['name']),patch.object(release,'api',side_effect=[self.run,checks]):
                with self.assertRaises(ValueError): release.qualified_run('123',SHA)

    def test_tag_conflict_never_mutates_public_history(self):
        with patch.object(release,'api',return_value={'object':{'type':'commit','sha':'b'*40}}) as api:
            with self.assertRaises(ValueError): release.publish(Path('unused'),{},'1.0.1',SHA,Path('unused'))
            api.assert_called_once_with('git/ref/tags/v1.0.1',missing=True)

    def test_asset_conflict_never_overwrites(self):
        ref = {'object':{'type':'commit','sha':SHA}}
        existing = {'id':1,'tag_name':'v1.0.1','prerelease':False,'draft':True,'assets':[{'name':'unexpected','id':2}]}
        with patch.object(release,'api',side_effect=[ref,existing]) as api:
            with self.assertRaises(ValueError): release.publish(Path('unused'),{'expected':'0'*64},'1.0.1',SHA,Path('unused'))
            self.assertEqual(api.call_count,2)

    def fixture(self, root, marker_sha=SHA, backend_hash=None):
        windows,linux=root/'windows',root/'linux'
        windows.mkdir();linux.mkdir()
        win_name='TDSBLive-1.0.1-win-x64.zip'
        marker={'formatVersion':1,'version':'1.0.1','sourceCommit':marker_sha,'target':'win-x64'}
        with zipfile.ZipFile(windows/win_name,'w') as archive:
            archive.writestr('TDSBLive.package.json',json.dumps(marker))
        (windows/'TDSBLive-1.0.1-win-x64-setup.exe').write_bytes(b'owned installer fixture')
        sums={path.name:release.digest(path) for path in windows.iterdir()}
        (windows/'SHA256SUMS.txt').write_text(''.join(f'{value}  {name}\n' for name,value in sums.items()))
        top='TDSBLive-1.0.1-linux-x64-wine/'
        native={'formatVersion':1,'version':'1.0.1','sourceCommit':SHA,'target':'linux-x64-wine',
                'backendArchive':win_name,'backendSha256':backend_hash or sums[win_name]}
        tar_path=linux/'TDSBLive-1.0.1-linux-x64-wine.tar.gz'
        with tarfile.open(tar_path,'w:gz') as archive:
            for name,value in [(top+'TDSBLive.package.json',native),(top+'backend/TDSBLive.package.json',marker)]:
                data=json.dumps(value).encode();item=tarfile.TarInfo(name);item.size=len(data)
                archive.addfile(item,io.BytesIO(data))
        (linux/'SHA256SUMS.txt').write_text(f'{release.digest(tar_path)}  {tar_path.name}\n')
        return windows,linux

    def test_packages_combine_three_verified_checksums(self):
        with tempfile.TemporaryDirectory() as temp,patch.object(release,'scan'):
            root=Path(temp);windows,linux=self.fixture(root)
            result=release.packages(windows,linux,root/'staged','1.0.1',SHA)
            self.assertEqual(len(result),4)
            self.assertEqual(len((root/'staged/SHA256SUMS.txt').read_text().splitlines()),3)

    def test_mismatched_commit_or_backend_archive_is_rejected_before_staging(self):
        for options in ({'marker_sha':'b'*40},{'backend_hash':'0'*64}):
            with self.subTest(options=options),tempfile.TemporaryDirectory() as temp,patch.object(release,'scan'):
                root=Path(temp);windows,linux=self.fixture(root,**options)
                with self.assertRaises(ValueError): release.packages(windows,linux,root/'staged','1.0.1',SHA)
                self.assertFalse((root/'staged').exists())

    def test_modified_asset_and_duplicate_checksum_rejected(self):
        for duplicate in (False,True):
            with self.subTest(duplicate=duplicate),tempfile.TemporaryDirectory() as temp,patch.object(release,'scan'):
                root=Path(temp);windows,linux=self.fixture(root)
                if duplicate:
                    path=windows/'SHA256SUMS.txt';path.write_text(path.read_text()*2)
                else: (windows/'TDSBLive-1.0.1-win-x64-setup.exe').write_bytes(b'changed fixture')
                with self.assertRaises(ValueError): release.packages(windows,linux,root/'staged','1.0.1',SHA)
                self.assertFalse((root/'staged').exists())


if __name__ == '__main__': unittest.main()
