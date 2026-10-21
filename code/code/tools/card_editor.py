"""Loopback-only card editor. Writes numeric AST literals to the authoritative design."""
import ast
import copy
import hashlib
import json
import os
from pathlib import Path
import secrets
import shutil
import subprocess
import sys
import tempfile
import threading
import time
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from urllib.parse import urlsplit
import webbrowser

import card_design

ROOT = Path(__file__).resolve().parents[1]
RARITIES = dict(Basic='基础', Common='普通', Uncommon='罕见', Rare='稀有', Ancient='先古')
KINDS = dict(Attack='单体伤害', AllAttack='全体伤害', AttackBonus='伤害与条件加成', Block='格挡', BlockBonus='格挡与条件加成',
    Draw='抽牌', Energy='获得能量', Signal='获得信号', EnergyDebt='能量透支', DrawDebt='抽牌透支', SignalDebt='信号透支',
    DamageDebt='伤害透支（%）', BlockDebt='格挡透支（%）', Weak='虚弱', AllWeak='全体虚弱', AllVulnerable='全体易伤',
    SignalAttack='消耗信号造成伤害', SignalBlock='消耗信号获得格挡', RepayEnergy='减少能量透支（99 为全部）', RepayDraw='减少抽牌透支（99 为全部）',
    Power='能力层数', Strength='获得力量', Dexterity='获得敏捷', SignalStrength='全部信号转为力量', SignalRare='消耗信号生成稀有牌（数值为每张耗量）', DebtAttack='按本场透支出牌次数攻击', Replay='重放此前卡牌', EchoSelf='试演次数', Ancient='生成先古牌', LastWill='遗志：全部资源乘积与延迟死亡', FightingChance='一线生机：零能量零信号限制与补充', LetGo='割舍：随机偿还与自选消耗')
KEYWORDS = dict(Exhaust=('消耗','打出后在本场战斗中移除。'), Ethereal=('虚无','回合结束时仍在手牌中则消耗。'),
    Innate=('固有','战斗开始时出现在起始手牌中。'), Retain=('保留','回合结束时不会自动弃掉。'),
    Unplayable=('不能被打出','无法手动打出。'), Sly=('奇巧','在回合结束前被弃掉时，免费打出。'),
    Eternal=('永恒','不能从牌组中移除或变化。'))


def digest(text):
    return hashlib.sha256(text.encode('utf-8')).hexdigest()


def arg(call, name, index=None, default=None):
    if index is not None and len(call.args) > index:
        return ast.literal_eval(call.args[index])
    keyword = next((k for k in call.keywords if k.arg == name), None)
    return ast.literal_eval(keyword.value) if keyword else default


def put(call, name, value, index=None):
    literal = ast.parse(repr(value), mode='eval').body
    if index is not None and len(call.args) > index:
        call.args[index] = literal
        return
    keyword = next((k for k in call.keywords if k.arg == name), None)
    if keyword:
        keyword.value = literal
    else:
        call.keywords.append(ast.keyword(arg=name, value=literal))


def validate_steps(steps):
    if not isinstance(steps, list) or len(steps)>32:raise ValueError('效果列表无效，最多32项')
    result=[]
    for raw in steps:
        if not isinstance(raw,dict) or set(raw)-{'kind','value','upgrade','x','ux','pay','cap','threshold','condition','power'}:
            raise ValueError('效果字段无效')
        s=dict(raw)
        if s.get('kind') not in KINDS:raise ValueError('未知效果类型')
        for field in ('value','upgrade','x','ux','pay','cap','threshold'):
            if field in s and (type(s[field]) is not int or not 0<=s[field]<=9999):raise ValueError('效果数值应为0至9999的整数')
        if 'value' not in s or 'upgrade' not in s:raise ValueError('效果缺少基础或升级值')
        if s.get('condition','') not in ('','replay','plays','lastAttack','lastSkill','debt','signal'):raise ValueError('未知触发条件')
        if s['kind']=='Power' and s.get('power') not in card_design.POWER_TEXT:raise ValueError('未知能力')
        result.append(s)
    return result


def set_steps(call, steps):
    nodes=[]
    for s in steps:
        nodes.append(ast.Call(func=ast.Name(id='op',ctx=ast.Load()), args=[ast.Constant(s['kind']),ast.Constant(s['value']),ast.Constant(s['upgrade'])],
            keywords=[ast.keyword(arg=k,value=ast.Constant(v)) for k,v in s.items() if k not in ('kind','value','upgrade')]))
    call.args[5]=ast.List(elts=nodes,ctx=ast.Load())


def normalize_call(call):
    steps=[]
    for node in call.args[5].elts:
        raw=dict(kind=arg(node,'kind',0),value=arg(node,'value',1,0),upgrade=arg(node,'upgrade',2,arg(node,'value',1,0)))
        raw.update({k.arg:ast.literal_eval(k.value) for k in node.keywords if k.arg not in ('value','upgrade')})
        normalized=card_design.normalize_step(raw)
        if normalized is not None:steps.append(normalized)
    set_steps(call,steps)
    call.keywords=[k for k in call.keywords if not (k.arg in ('replays','signalReplayCost','playedReplayThreshold') and ast.literal_eval(k.value)==0)]
    if arg(call,'keywords',default=None)==[]:call.keywords=[k for k in call.keywords if k.arg!='keywords']
    if arg(call,'upgradeKeywords',default=None)==[]:call.keywords=[k for k in call.keywords if k.arg!='upgradeKeywords']


def read_cards(source):
    cards, nodes = [], {}
    for statement in ast.parse(source).body:
        if not isinstance(statement, ast.Expr) or not isinstance(statement.value, ast.Call):
            continue
        call = statement.value
        if not isinstance(call.func, ast.Name) or call.func.id != 'card':
            continue
        key = 'Geopelia' + arg(call, 'suffix', 0)
        cost = arg(call, 'cost', 3)
        card = dict(className=key, name=arg(call, 'name', 1), rarity=arg(call, 'rarity', 2), cost=cost,
            upgradeCost=arg(call, 'upgradeCost', default=cost), type=arg(call, 'type', 4), steps=[],
            replays=arg(call, 'replays', default=0), signalReplayCost=arg(call, 'signalReplayCost', default=0),
            playedReplayThreshold=arg(call, 'playedReplayThreshold', default=0), keywords=arg(call, 'keywords', default=[]),
            themes=arg(call, 'theme', 6).split('/'))
        for optional in ('upgradeKeywords','descriptionOverride','costsX','custom'):
            value=arg(call,optional,default=None)
            if value is not None:card[optional]=value
        for step in call.args[5].elts:
            if not isinstance(step, ast.Call) or not isinstance(step.func, ast.Name) or step.func.id != 'op':
                raise ValueError('设计包含无法编辑的效果表达式')
            value = arg(step, 'value', 1, 0)
            effect = dict(kind=arg(step, 'kind', 0), value=value, upgrade=arg(step, 'upgrade', 2, value))
            effect.update({k.arg: ast.literal_eval(k.value) for k in step.keywords if k.arg not in ('value', 'upgrade')})
            card['steps'].append(effect)
        if key in nodes:
            raise ValueError('重复卡牌标识')
        nodes[key] = call
        cards.append(card)
    if len(cards) != sum(card_design.EXPECTED.values()):
        raise ValueError('卡牌数量与设计清单不符')
    return cards, nodes


def numeric_fields(card):
    fields = {k: (0, 99) for k in ('cost', 'upgradeCost', 'playedReplayThreshold')}
    fields.update(replays=(0, 99), signalReplayCost=(0, 99))
    for i, step in enumerate(card['steps']):
        for key in ('value', 'upgrade'):
            fields[f'steps.{i}.{key}'] = (0, 9999)
        for key in ('x', 'ux', 'pay', 'cap', 'threshold'):
            if key in step or key=='pay' or (key in ('x','ux') and step['kind'] in ('AttackBonus','BlockBonus','SignalAttack','SignalBlock')) or (key=='cap' and step['kind'] in ('SignalAttack','SignalBlock')) or (key=='threshold' and step.get('condition') in ('plays','signal')):
                fields[f'steps.{i}.{key}'] = (0, 9999)
    return fields


def patch_source(source, patches):
    if not isinstance(patches, list) or len(patches) > 5000:
        raise ValueError('修改列表无效')
    cards, nodes = read_cards(source)
    by_id = {c['className']: c for c in cards}
    changed = set()
    for patch in patches:
        if not isinstance(patch, dict) or set(patch) != {'card', 'field', 'value'}:
            raise ValueError('修改项格式无效')
        card, field, value = patch['card'], patch['field'], patch['value']
        if not isinstance(card, str) or card not in nodes or not isinstance(field, str):
            raise ValueError('卡牌或字段无效')
        call = nodes[card]
        if field=='steps':
            set_steps(call,validate_steps(value));changed.add(card);continue
        if field.startswith(('keywords.','upgradeKeywords.')):
            group,keyword=field.split('.',1)
            if keyword not in KEYWORDS or type(value) is not bool:raise ValueError('关键词无效')
            keywords=arg(call,group,default=[])
            keywords=[k for k in keywords if k!=keyword]
            if value:keywords.append(keyword)
            put(call,group,keywords);changed.add(card);continue
        bounds = numeric_fields(by_id[card]).get(field)
        if not bounds or type(value) is not int or not bounds[0] <= value <= bounds[1]:
            raise ValueError('请输入允许范围内的整数：' + field)
        # Freeze inherited upgrade values before changing a base value, so each input is independent.
        if field == 'cost':
            put(call, 'upgradeCost', arg(call, 'upgradeCost', default=by_id[card]['upgradeCost']))
        if field.startswith('steps.'):
            _, index, key = field.split('.')
            step = call.args[5].elts[int(index)]
            if key == 'value':
                put(step, 'upgrade', arg(step, 'upgrade', 2, by_id[card]['steps'][int(index)]['upgrade']), 2)
            if key == 'x':
                original = by_id[card]['steps'][int(index)]
                put(step, 'ux', arg(step, 'ux', default=original.get('ux', original['x'])))
            put(step, key, value, {'value': 1, 'upgrade': 2}.get(key))
        else:
            put(call, field, value, 3 if field == 'cost' else None)
        changed.add(card)
    # Replace only changed card calls. Preserve the rest of the authored Python file verbatim.
    lines = source.splitlines(keepends=True)
    offsets = [0]
    for line in lines:
        offsets.append(offsets[-1] + len(line.encode('utf-8')))
    edits = []
    for key in changed:
        node = nodes[key]
        normalize_call(node)
        edits.append((offsets[node.lineno - 1] + node.col_offset,
            offsets[node.end_lineno - 1] + node.end_col_offset, ast.unparse(node).encode('utf-8')))
    result = source.encode('utf-8')
    for start, end, replacement in sorted(edits, reverse=True):
        result = result[:start] + replacement + result[end:]
    result = result.decode('utf-8')
    read_cards(result)
    return result


def decorate(cards):
    for card in cards:
        for step in card['steps']:
            if step['kind'] == 'Power':
                old = card_design.POWER_TEXT[step['power']]
                card_design.POWER_TEXT[step['power']] = (card['name'], old[1])
    for card in cards:
        descriptions = []
        for upgraded in (False, True):
            text=card_design.card_text(card,upgraded=upgraded,include_replay=True)
            for i, step in enumerate(card['steps']):
                text = text.replace('{V%d:diff()}' % i, str(step['upgrade'] if upgraded else step['value']))
                text = text.replace('{X%d:diff()}' % i, str(step.get('ux', step.get('x', 0)) if upgraded else step.get('x', 0)))
            for tag in ('[gold]', '[/gold]', '[red]', '[/red]'):text=text.replace(tag,'')
            descriptions.append(text)
        card['descriptions'] = descriptions
        card['rarityLabel'] = RARITIES[card['rarity']]
        for step in card['steps']:
            step['label'] = KINDS.get(step['kind'], step['kind'])
    return cards


class Editor:
    def __init__(self, source=None, backup_dir=None):
        self.source = Path(source or ROOT / 'tools/card_design.py')
        self.backup_dir = Path(backup_dir or ROOT.parents[1] / '.modding-backups/PhigrosJiu-card-edits')
        self.lock = threading.RLock()
        self.job = None
        self.art = {c['className']: ROOT / c['sourceFile']
                    for c in json.loads((ROOT / 'content/artwork-map.json').read_text(encoding='utf-8'))['cards']}

    def text(self):
        return self.source.read_bytes().decode('utf-8-sig')

    def state(self, source=None):
        with self.lock:
            text = self.text() if source is None else source
            cards, _ = read_cards(text)
            compatible=[]
            revisions=ROOT/'build/card-editor/source-revisions.json'
            if self.source.resolve()==(ROOT/'tools/card_design.py').resolve() and revisions.exists():
                fingerprint=digest(json.dumps(cards,sort_keys=True,ensure_ascii=False))
                compatible=[key for key,value in json.loads(revisions.read_text(encoding='utf-8')).items() if value==fingerprint]
            return dict(revision=digest(text), compatibleRevisions=compatible, cards=decorate(cards), source=str(self.source), job=self.job_state(),
                keywordOptions=[dict(key=k,label=v[0],description=v[1]) for k,v in KEYWORDS.items()],effectKinds=KINDS,powerOptions={k:v[0] for k,v in card_design.POWER_TEXT.items()})

    def candidate(self, payload):
        text = self.text()
        if payload.get('revision') != digest(text) and payload.get('revision') not in self.state()['compatibleRevisions']:
            raise ValueError('设计文件已被其他编辑器修改。请先导出未保存修改，再重新载入。')
        return patch_source(text, payload.get('patches'))

    def save(self, payload):
        with self.lock:
            if self.job and self.job['status'] == 'running':
                raise ValueError('正在构建，请完成后再保存')
            candidate = self.candidate(payload)
            original = self.text()
            if candidate == original:
                return dict(**self.state(), backup=None)
            self.backup_dir.mkdir(parents=True, exist_ok=True)
            folder = self.backup_dir / (time.strftime('%Y%m%d-%H%M%S') + '-' + secrets.token_hex(3))
            folder.mkdir()
            (folder / 'card_design.py').write_bytes(self.source.read_bytes())
            (folder / 'changes.json').write_text(json.dumps(payload, ensure_ascii=False, indent=2), encoding='utf-8')
            temp_name = None
            try:
                with tempfile.NamedTemporaryFile(dir=self.source.parent, delete=False, suffix='.tmp') as temp:
                    temp_name = temp.name
                    temp.write(candidate.encode('utf-8'))
                    temp.flush()
                    os.fsync(temp.fileno())
                os.replace(temp_name, self.source)
            finally:
                if temp_name and Path(temp_name).exists():
                    Path(temp_name).unlink()
            return dict(**self.state(), backup=str(folder))

    def job_state(self):
        if not self.job:
            return None
        state = {k: v for k, v in self.job.items() if k != 'process'}
        if Path(state['log']).exists():
            state['output'] = Path(state['log']).read_bytes()[-16000:].decode('utf-8', errors='replace')
        return state

    def build(self, revision):
        with self.lock:
            if revision != digest(self.text()):
                raise ValueError('请重新载入最新设计后构建')
            if self.job and self.job['status'] == 'running':
                raise ValueError('已有构建正在运行')
            log_dir = ROOT / 'build/editor-logs'
            log_dir.mkdir(parents=True, exist_ok=True)
            log = log_dir / (time.strftime('%Y%m%d-%H%M%S') + '.log')
            shell = shutil.which('pwsh') or shutil.which('powershell')
            if not shell:
                raise ValueError('未找到 PowerShell')
            env = os.environ.copy()
            env['PATH'] = str(Path(sys.executable).parent) + os.pathsep + env.get('PATH', '')
            env['PYTHONIOENCODING'] = 'utf-8'
            with log.open('wb') as stream:
                process = subprocess.Popen([shell, '-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', str(ROOT / 'build.ps1')],
                    cwd=ROOT, env=env, stdout=stream, stderr=subprocess.STDOUT,
                    creationflags=getattr(subprocess, 'CREATE_NO_WINDOW', 0))
            self.job = dict(status='running', log=str(log), process=process, revision=revision, started=time.time())
            def finish():
                code = process.wait()
                with self.lock:
                    self.job.update(status='success' if code == 0 else 'failed', exitCode=code)
            threading.Thread(target=finish, daemon=True).start()
            return self.job_state()


def make_server(editor, port=18770, token=None):
    token = token or secrets.token_urlsafe(32)
    class Handler(BaseHTTPRequestHandler):
        def log_message(self, *args):
            pass

        def reply(self, body, status=200, mime='application/json; charset=utf-8'):
            if not isinstance(body, bytes):
                body = json.dumps(body, ensure_ascii=False).encode('utf-8')
            self.send_response(status)
            self.send_header('Content-Type', mime)
            self.send_header('Content-Length', str(len(body)))
            self.send_header('X-Content-Type-Options', 'nosniff')
            self.send_header('X-Frame-Options', 'DENY')
            self.send_header('Cache-Control', 'no-store' if not mime.startswith('image/') else 'private, max-age=3600')
            self.end_headers()
            self.wfile.write(body)

        def allowed(self, api=False):
            expected = f'127.0.0.1:{self.server.server_port}'
            if self.headers.get('Host') != expected:
                self.reply(dict(error='只接受本机访问'), 403)
                return False
            origin = self.headers.get('Origin')
            if origin and origin != 'http://' + expected:
                self.reply(dict(error='跨站请求已拒绝'), 403)
                return False
            if api and not secrets.compare_digest(self.headers.get('X-Jiu-Token', ''), token):
                self.reply(dict(error='会话已失效，请重新打开编辑器'), 403)
                return False
            return True

        def do_GET(self):
            path = urlsplit(self.path).path
            if not self.allowed(path.startswith('/api/')):
                return
            if path == '/':
                page = (ROOT / 'tools/card_editor_v2.html').read_text(encoding='utf-8').replace('__SESSION_TOKEN__', token)
                self.reply(page.encode('utf-8'), mime='text/html; charset=utf-8')
            elif path == '/api/state':
                self.reply(editor.state())
            elif path == '/api/job':
                self.reply(dict(job=editor.job_state()))
            elif path.startswith('/art/') and path[5:] in editor.art:
                self.reply(editor.art[path[5:]].read_bytes(), mime='image/png')
            else:
                self.reply(dict(error='未找到'), 404)

        def do_POST(self):
            if not self.allowed(True):
                return
            try:
                size = int(self.headers.get('Content-Length', '0'))
                if not 0 < size <= 1000000:
                    raise ValueError('请求大小无效')
                data = json.loads(self.rfile.read(size))
                if not isinstance(data, dict):
                    raise ValueError('请求格式无效')
                with editor.lock:
                    if self.path == '/api/preview':
                        self.reply(editor.state(editor.candidate(data)))
                    elif self.path == '/api/save':
                        self.reply(editor.save(data))
                    elif self.path == '/api/build':
                        self.reply(dict(job=editor.build(data.get('revision'))))
                    else:
                        self.reply(dict(error='未找到'), 404)
            except (ValueError, SyntaxError, KeyError, TypeError) as error:
                self.reply(dict(error=str(error)), 400)
            except Exception as error:
                self.reply(dict(error='操作失败：' + str(error)), 500)

    server = ThreadingHTTPServer(('127.0.0.1', port), Handler)
    server.editor_token = token
    return server


def main():
    import argparse
    parser = argparse.ArgumentParser()
    parser.add_argument('--port', type=int, default=18770)
    parser.add_argument('--no-browser', action='store_true')
    args = parser.parse_args()
    server = make_server(Editor(), args.port, os.environ.get('JIU_EDITOR_TOKEN'))
    url = f'http://127.0.0.1:{server.server_port}/'
    print('JIU_CARD_EDITOR=' + url, flush=True)
    if not args.no_browser:
        webbrowser.open(url)
    try:
        server.serve_forever()
    except KeyboardInterrupt:
        pass
    finally:
        server.server_close()


if __name__ == '__main__':
    main()
