import copy
import http.client
import importlib.util
import json
from pathlib import Path
import sys
import tempfile
import threading
import unittest

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / 'tools'))
import card_editor as ce


class CardEditorTests(unittest.TestCase):
    def setUp(self):
        (ROOT / 'build').mkdir(exist_ok=True)
        self.temp = tempfile.TemporaryDirectory(dir=ROOT / 'build')
        self.addCleanup(self.temp.cleanup)
        self.source = Path(self.temp.name) / 'card_design.py'
        self.original = (ROOT / 'tools/card_design.py').read_text(encoding='utf-8')
        self.source.write_text(self.original, encoding='utf-8')
        self.editor = ce.Editor(self.source, Path(self.temp.name) / 'backups')

    def payload(self, field='steps.0.value', value=9, card='GeopeliaStrike'):
        return dict(revision=self.editor.state()['revision'], patches=[dict(card=card, field=field, value=value)])

    def test_source_roundtrip_and_upgrade_order(self):
        payload = self.payload()
        payload['patches'] = [dict(card='GeopeliaStrike', field='steps.0.upgrade', value=14),
            dict(card='GeopeliaStrike', field='steps.0.value', value=9)]
        after = ce.patch_source(self.original, payload['patches'])
        before_cards, _ = ce.read_cards(self.original)
        after_cards, _ = ce.read_cards(after)
        expected = copy.deepcopy(before_cards)
        expected[0]['steps'][0].update(value=9, upgrade=14)
        self.assertEqual(expected, after_cards)
        self.assertIn("card('Defend','Glaciaxion'", after)

    def test_inherited_upgrade_stays_independent(self):
        after = ce.patch_source(self.original, [dict(card='GeopeliaStrike', field='cost', value=2)])
        cards, _ = ce.read_cards(after)
        self.assertEqual((2, 1), (cards[0]['cost'], cards[0]['upgradeCost']))
        source = self.original.replace("op('Attack',7,10)", "op('Attack',7)", 1)
        cards, _ = ce.read_cards(ce.patch_source(source, self.payload(value=11)['patches']))
        self.assertEqual((11, 7), (cards[0]['steps'][0]['value'], cards[0]['steps'][0]['upgrade']))

    def test_numeric_edits_across_all_unicode_song_titles(self):
        cards, _ = ce.read_cards(self.original)
        patches = [dict(card=c['className'], field='steps.0.value', value=c['steps'][0]['value'] + 1) for c in cards]
        result, _ = ce.read_cards(ce.patch_source(self.original, patches))
        expected = copy.deepcopy(cards)
        for card in expected:
            card['steps'][0]['value'] += 1
            card['steps']=[s for raw in card['steps'] if (s:=ce.card_design.normalize_step(raw)) is not None]
        self.assertEqual(expected, result)

    def test_zero_removes_effect_but_keeps_zero_cost_and_upgrade_only_effect(self):
        patches=[dict(card='GeopeliaStrike',field='cost',value=0),dict(card='GeopeliaStrike',field='upgradeCost',value=0),
            dict(card='GeopeliaStrike',field='steps.0.value',value=0),dict(card='GeopeliaStrike',field='steps.0.upgrade',value=0)]
        cards,_=ce.read_cards(ce.patch_source(self.original,patches))
        self.assertEqual([],cards[0]['steps']);self.assertEqual((0,0),(cards[0]['cost'],cards[0]['upgradeCost']))
        cards,_=ce.read_cards(ce.patch_source(self.original,patches[:-1]))
        preview=ce.decorate(cards)[0]
        self.assertEqual('',preview['descriptions'][0]);self.assertIn('10点伤害',preview['descriptions'][1])
        self.assertIn('IfUpgraded:show:',ce.card_design.card_text(cards[0]))

    def test_keywords_and_effect_addition_and_removal(self):
        patches=[dict(card='GeopeliaStrike',field='keywords.Ethereal',value=True),dict(card='GeopeliaStrike',field='keywords.Exhaust',value=True),
            dict(card='GeopeliaStrike',field='steps',value=[dict(kind='Draw',value=2,upgrade=3)])]
        source=ce.patch_source(self.original,patches)
        cards,_=ce.read_cards(source)
        self.assertEqual(['Ethereal','Exhaust'],cards[0]['keywords']);self.assertEqual('Draw',cards[0]['steps'][0]['kind'])
        source=ce.patch_source(source,[dict(card='GeopeliaStrike',field='keywords.Ethereal',value=False),dict(card='GeopeliaStrike',field='steps',value=[])])
        cards,_=ce.read_cards(source)
        self.assertEqual(['Exhaust'],cards[0]['keywords']);self.assertEqual([],cards[0]['steps'])
        with self.assertRaises(ValueError):ce.patch_source(source,[dict(card='GeopeliaStrike',field='steps',value=[dict(kind='ExecutePython',value=1,upgrade=1)])])

    def test_replay_is_not_duplicated_and_zero_payment_hidden(self):
        cards,_=ce.read_cards(self.original)
        repeat=next(c for c in cards if c['replays']>0)
        self.assertNotIn('额外打出',ce.card_design.card_text(repeat))
        self.assertIn('重放',ce.card_design.card_text(repeat,include_replay=True))
        text=ce.card_design.step_text(dict(kind='AttackBonus',value=1,upgrade=1,x=2,ux=2,condition='signal',threshold=0,pay=0),0,upgraded=False)
        self.assertNotIn('0点',text)

    def test_draw_debt_repayment_roundtrip_preview_and_zero(self):
        steps=[dict(kind='RepayDraw',value=2,upgrade=99)]
        payload=self.payload('steps',steps)
        preview=self.editor.state(self.editor.candidate(payload))
        self.assertIn('RepayDraw',preview['effectKinds'])
        self.assertEqual(['减少2张下回合的抽牌透支。','清除下回合全部抽牌透支。'],preview['cards'][0]['descriptions'])
        self.editor.save(payload)
        self.assertEqual(steps,[
            {k:v for k,v in s.items() if k!='label'} for s in self.editor.state()['cards'][0]['steps']])
        payload=self.payload('steps',[dict(kind='RepayDraw',value=0,upgrade=99)])
        preview=self.editor.state(self.editor.candidate(payload))
        self.assertEqual('',preview['cards'][0]['descriptions'][0])
        self.assertEqual('清除下回合全部抽牌透支。',preview['cards'][0]['descriptions'][1])
        self.editor.save(self.payload('steps',[dict(kind='RepayDraw',value=0,upgrade=0)]))
        self.assertEqual([],self.editor.state()['cards'][0]['steps'])

    def test_song_rework_metadata_and_descriptions(self):
        cards,_=ce.read_cards(self.original)
        by_id={c['className']:c for c in cards}
        old=by_id['GeopeliaOldRecording']
        self.assertEqual('不息的信号',ce.card_design.card_text(old,include_replay=True))
        changed=ce.patch_source(self.original,[dict(card='GeopeliaOldRecording',field='cost',value=1)])
        preserved=next(c for c in ce.read_cards(changed)[0] if c['className']=='GeopeliaOldRecording')
        self.assertEqual(old['steps'],preserved['steps'])
        nhelv=by_id['GeopeliaCounterpoint']
        self.assertEqual(['Exhaust'],nhelv['keywords'])
        self.assertEqual(['Retain'],nhelv['upgradeKeywords'])
        source=ce.patch_source(self.original,[dict(card='GeopeliaCounterpoint',field='cost',value=0)])
        saved=next(c for c in ce.read_cards(source)[0] if c['className']=='GeopeliaCounterpoint')
        self.assertEqual(['Retain'],saved['upgradeKeywords'])
        source=ce.patch_source(source,[dict(card='GeopeliaCounterpoint',field='upgradeKeywords.Retain',value=False)])
        saved=next(c for c in ce.read_cards(source)[0] if c['className']=='GeopeliaCounterpoint')
        self.assertEqual([],saved.get('upgradeKeywords',[]))
        self.assertEqual((2,1),(by_id['GeopeliaSustainAmplifier']['cost'],by_id['GeopeliaSustainAmplifier']['upgradeCost']))
        self.assertEqual((2,2),(by_id['GeopeliaOpeningTheme']['cost'],by_id['GeopeliaOpeningTheme']['upgradeCost']))
        for name,(_,body) in ce.card_design.POWER_TEXT.items():
            if name!='Archidoxen':self.assertNotIn('每回合最多',body)

    def test_preview_and_backup_save(self):
        payload = self.payload()
        preview = self.editor.state(self.editor.candidate(payload))
        self.assertIn('9点伤害', preview['cards'][0]['descriptions'][0])
        self.assertEqual(self.original, self.source.read_text(encoding='utf-8'))
        original_bytes = self.source.read_bytes()
        saved = self.editor.save(payload)
        self.assertNotEqual(payload['revision'], saved['revision'])
        self.assertEqual(original_bytes, (Path(saved['backup']) / 'card_design.py').read_bytes())
        self.assertEqual(9, self.editor.state()['cards'][0]['steps'][0]['value'])

    def test_stale_revision_and_invalid_values_never_write(self):
        payload = self.payload()
        self.source.write_text(self.original + '\n# other editor\n', encoding='utf-8')
        with self.assertRaises(ValueError):
            self.editor.save(payload)
        for field, value in [('name', 1), ('steps.0.kind', 2), ('cost', -1), ('cost', True), ('cost', 0.5), ('steps.0.value', 10000)]:
            with self.assertRaises(ValueError):
                ce.patch_source(self.original, self.payload(field, value)['patches'])

    def test_http_auth_origin_and_save(self):
        server = ce.make_server(self.editor, 0)
        thread = threading.Thread(target=server.serve_forever, daemon=True)
        thread.start()
        self.addCleanup(server.server_close)
        self.addCleanup(server.shutdown)
        def request(path, body=None, token=True, origin=None, host=None):
            conn = http.client.HTTPConnection('127.0.0.1', server.server_port)
            headers = {'X-Jiu-Token': server.editor_token} if token else {}
            if origin: headers['Origin'] = origin
            if host: headers['Host'] = host
            conn.request('POST' if body is not None else 'GET', path, body=json.dumps(body) if body is not None else None, headers=headers)
            response = conn.getresponse()
            data = response.read()
            conn.close()
            return response.status, data
        self.assertEqual(403, request('/api/state', token=False)[0])
        self.assertEqual(403, request('/api/save', self.payload(), origin='https://example.com')[0])
        self.assertEqual(403, request('/', host='evil.example')[0])
        self.assertEqual(404, request('/art/../../card_design.py')[0])
        self.assertEqual(200, request('/art/GeopeliaStrike')[0])
        self.assertEqual(200, request('/api/save', self.payload())[0])
        self.assertEqual(9, self.editor.state()['cards'][0]['steps'][0]['value'])

    def test_changed_design_does_not_reuse_native_measurements(self):
        import contextlib
        import io
        import shutil
        sandbox = Path(self.temp.name) / 'audit'
        for folder in ['content', 'research', 'src', 'build']:
            (sandbox / folder).mkdir(parents=True)
        for relative in ['content/cards.json', 'content/artwork-map.json', 'research/balance-030-cards.json', 'research/info.tsv', 'src/Powers.cs']:
            shutil.copyfile(ROOT / relative, sandbox / relative)
        # A stale measurement fixture must not depend on this computer's ignored test cache.
        (sandbox/'build/native-validation.json').write_text(json.dumps(dict(result='PASS',economy=[dict(Model='GeopeliaAdvance',Upgraded=False,NetEnergy=-999,NetDraw=-999)])),encoding='utf-8')
        (sandbox/'build/native-validation-build.json').write_text(json.dumps(dict(validation_mode='full',design_sha256='0'*64)),encoding='utf-8')
        path = sandbox / 'content/cards.json'
        cards = json.loads(path.read_text(encoding='utf-8'))
        next(c for c in cards if c['className'] == 'GeopeliaAdvance')['steps'][0]['value'] = 0
        path.write_text(json.dumps(cards, ensure_ascii=False), encoding='utf-8')
        script = (ROOT / 'tools/audit_economy.py').read_text(encoding='utf-8').replace('ROOT = Path(__file__).resolve().parents[1]', 'ROOT = Path(' + repr(str(sandbox)) + ')')
        with contextlib.redirect_stdout(io.StringIO()):
            exec(compile(script, 'audit_economy.py', 'exec'), {'__file__': str(ROOT / 'tools/audit_economy.py')})
        result = json.loads((sandbox / 'content/economy-audit.json').read_text(encoding='utf-8'))
        self.assertFalse(result['nativeMeasurementsCurrent'])
        self.assertEqual([], result['nativeComparisons'])
        self.assertGreater(len(result['balanceWarnings']), 0)


if __name__ == '__main__':
    unittest.main()
