"""Exercise delivery failure modes without starting .NET or changing real artifacts."""
import hashlib
import importlib.util
import json
import pathlib
import tempfile
import unittest
import zipfile

spec=importlib.util.spec_from_file_location('bundle_verifier',pathlib.Path(__file__).parents[1]/'Verify-ChartsPreviewBundle.py')
verifier=importlib.util.module_from_spec(spec);spec.loader.exec_module(verifier)


class BundleTests(unittest.TestCase):
    def setUp(self):
        self.temp=tempfile.TemporaryDirectory(dir=pathlib.Path(__file__).resolve().parent,prefix='charts-preview-bundle-test-');self.addCleanup(self.temp.cleanup)
        self.root=pathlib.Path(self.temp.name)/'charts-preview-test';self.root.mkdir();(self.root/'packages').mkdir()
        self.names=['NT.AvaloniaControl.Charts','NT.AvaloniaControl.Charts.Themes','NT.AvaloniaControl.Charts.Skia'];self.version='0.2.0-preview.3'
        packages=[]
        for name in self.names:
            path=self.root/'packages'/f'{name}.{self.version}.nupkg';dll=name.encode()
            deps='<dependencies><group><dependency id="Avalonia" version="[12.0.3]"/><dependency id="Avalonia.Skia" version="[12.0.3]"/></group></dependencies>' if name.endswith('.Skia') else ''
            with zipfile.ZipFile(path,'w') as archive:
                archive.writestr(name+'.nuspec',f'<package><metadata><id>{name}</id><version>{self.version}</version>{deps}</metadata></package>')
                archive.writestr(f'lib/net10.0/{name}.dll',dll)
            packages.append(dict(path='C:\\old-machine\\'+path.name,sha256=verifier.digest(path),dllSha256=hashlib.sha256(dll).hexdigest().upper()))
        self.data=dict(version=self.version,packages=packages,readyForPrivateTrial=False,readyForPublicRelease=False,remotePushPerformed=False,originalC7MilestoneComplete=False,checks=[dict(passed=True)],blockers=['linux soak running'],phases={p:{'soak-v1':dict(status='running')} for p in ('windows','linux')})
        self.save_data()
        (self.root/'charts-preview-release-scope.json').write_text(json.dumps(dict(releaseVersion=self.version,packages=self.names)),encoding='utf-8')
        (self.root/'README.md').write_text('[Results](charts-preview-release-validation.json)\n',encoding='utf-8')
        self.rehash()

    def save_data(self):
        for name in ('validation.json','charts-preview-release-validation.json'):(self.root/name).write_text(json.dumps(self.data),encoding='utf-8')

    def rehash(self):
        (self.root/'SHA256SUMS').write_text(''.join(verifier.digest(p)+'  '+str(p.relative_to(self.root)).replace('\\','/')+'\n' for p in sorted(self.root.rglob('*')) if p.is_file() and p.name!='SHA256SUMS'),encoding='utf-8')

    def test_relocated_pending_bundle_integrity_is_valid(self):
        self.assertTrue(verifier.verify(self.root)['passed'])

    def test_pending_cannot_be_release_ready(self):
        self.assertFalse(verifier.verify(self.root,True)['passed'])

    def test_tampered_package_rejected(self):
        path=next((self.root/'packages').glob('*.nupkg'));path.write_bytes(path.read_bytes()+b'tampered')
        self.assertFalse(verifier.verify(self.root)['passed'])

    def test_broken_link_rejected_even_with_updated_manifest(self):
        (self.root/'README.md').write_text('[Results](missing.json)',encoding='utf-8');self.rehash()
        self.assertFalse(verifier.verify(self.root)['passed'])

    def test_unlisted_extra_file_rejected(self):
        (self.root/'extra.txt').write_text('unexpected',encoding='utf-8')
        self.assertFalse(verifier.verify(self.root)['passed'])

    def test_forged_readiness_rejected(self):
        self.data['readyForPrivateTrial']=True;self.data['blockers']=[];self.save_data();self.rehash()
        self.assertFalse(verifier.verify(self.root)['passed'])

    def test_inconsistent_validation_alias_rejected(self):
        (self.root/'charts-preview-release-validation.json').write_text('{}',encoding='utf-8');self.rehash()
        self.assertFalse(verifier.verify(self.root)['passed'])

    def test_complete_qualification_allows_private_readiness(self):
        self.data.update(readyForPrivateTrial=True,blockers=[])
        for platform in ('windows','linux'):self.data['phases'][platform]['soak-v1']=dict(status='passed',oracle=dict(passed=True,soakQualified=True,actualSeconds=28800.1))
        self.save_data();self.rehash()
        self.assertTrue(verifier.verify(self.root,True)['passed'])

    def test_archive_content_checked(self):
        path=pathlib.Path(self.temp.name)/'candidate.zip'
        with zipfile.ZipFile(path,'w') as archive:
            for item in self.root.rglob('*'):
                if item.is_file():archive.write(item,str(item.relative_to(self.root.parent)).replace('\\','/'))
        self.assertTrue(verifier.verify(self.root,archive_path=path)['passed'])
        with zipfile.ZipFile(path,'a') as archive:archive.writestr(self.root.name+'/unexpected.txt','extra')
        self.assertFalse(verifier.verify(self.root,archive_path=path)['passed'])


if __name__=='__main__':unittest.main()
