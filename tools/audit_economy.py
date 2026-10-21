"""Audit exact song names, stable identities, immediate benefits and full nominal resource debts."""
import json
import hashlib
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
cards = json.loads((ROOT/'content/cards.json').read_text(encoding='utf-8-sig'))
old = json.loads((ROOT/'research/balance-030-cards.json').read_text(encoding='utf-8-sig'))
previous = {c['className']:c for c in old}
name_by_class={c['className']:c['name'] for c in cards}
def title(suffix):return name_by_class['Geopelia'+suffix]
artwork = {c['className']:c for c in json.loads((ROOT/'content/artwork-map.json').read_text(encoding='utf-8'))['cards']}
song_titles = {r.split('\t')[1].strip():r.split('\t')[0] for r in (ROOT/'research/info.tsv').read_text(encoding='utf-8-sig').splitlines() if '\t' in r}
checks = 0
def check(test, message):
    global checks
    checks += 1
    assert test, message

check(len(cards)==85 and len({c['name'] for c in cards})==85, '85 distinct cards and titles')
for c in cards:
    check(c['name'] in song_titles, 'Not a Phigros title: '+c['name'])
    check(c['entry']==previous[c['className']]['entry'], 'Model ID changed: '+c['className'])
    check(c['portrait']==artwork[c['className']]['portrait'] and c['songId']==artwork[c['className']]['songId'], 'Portrait and song mismatch')
    check(not any(s['kind']=='NextEnergy' for s in c['steps']), 'Delayed energy reward')
check('AfterPlayerTurnStart' not in (ROOT/'src/Powers.cs').read_text(encoding='utf-8'), 'Turn-start resource reward')
check('PaidDebt' not in (ROOT/'src/Powers.cs').read_text(encoding='utf-8'), 'Repayment reward is delayed')

def budget(c, up):
    amounts={k:0 for k in ['Energy','Draw','EnergyDebt','DrawDebt']}
    for s in c['steps']:
        if s['kind'] not in amounts or s.get('pay',0):continue
        count=1+c.get('replays',0)
        if s.get('condition')=='replay':count=c.get('replays',0)
        elif s.get('condition'):continue
        amounts[s['kind']]+=(s['upgrade'] if up else s['value'])*count
    cost=c['upgradeCost'] if up else c['cost']
    return dict(cost=cost,**amounts,netEnergy=amounts['Energy']-cost-amounts['EnergyDebt'],netDraw=amounts['Draw']-1-amounts['DrawDebt'])

rows=[]
for c in cards:
    for up in [False,True]:
        rows.append(dict(model=c['className'],name=c['name'],upgraded=up,**budget(c,up)))
balance_warnings=[]
for model in ['Advance','BorrowedLight','MomentarySunrise']:
    c=next(c for c in cards if c['className']=='Geopelia'+model)
    for up in [False,True]:
        if budget(c,up)['netEnergy']<2:balance_warnings.append(c['name']+('+' if up else '')+': net energy below original design target')
native_path=ROOT/'build/native-validation.json'
native=json.loads(native_path.read_text(encoding='utf-8')) if native_path.exists() else {}
proof_path=ROOT/'build/native-validation-build.json'
proof=json.loads(proof_path.read_text(encoding='utf-8-sig')) if proof_path.exists() else {}
design_sha=hashlib.sha256((ROOT/'content/cards.json').read_bytes()).hexdigest()
native_current=native.get('result')=='PASS' and bool(native.get('economy')) and proof.get('validation_mode','full')=='full' and proof.get('design_sha256','').lower()==design_sha
# Historic measurements cannot reject a deliberate manual edit to the design.
native_rows=native.get('economy',[]) if native_current else []
for r in native_rows:
    if r['Model'] not in previous:continue
    predicted=next(x for x in rows if x['model']==r['Model'] and x['upgraded']==r['Upgraded'])
    check(predicted['netEnergy']==r['NetEnergy'] and predicted['netDraw']==r['NetDraw'], 'Actual engine budget differs: '+r['Model'])

def count(source, kind):return sum(any(s['kind']==kind and (s['value']>0 or s['upgrade']>0) for s in c['steps']) for c in source)
audit=dict(result='PASS',checks=checks,counts=dict(energyCardsBefore=count(old,'Energy'),energyCardsAfter=count(cards,'Energy'),
    drawCardsBefore=count(old,'Draw'),drawCardsAfter=count(cards,'Draw')), rows=rows,nativeComparisons=native_rows,
    nativeMeasurementsCurrent=native_current,designSha256=design_sha,balanceWarnings=balance_warnings)
(ROOT/'content/economy-audit.json').write_text(json.dumps(audit,ensure_ascii=False,indent=2),encoding='utf-8')
lines=['# 0.5.0 资源净收益核对','',
    '目标是略高于原版、接近无色牌的运转强度。透支是完整负值，延期本身不算收益。','',
    '能量净收益＝当回合加费−实际牌费−下回合能量透支；手牌净收益＝当回合实际抽牌−打出本牌的1张−下回合抽牌透支。', '',
    '净值不折算伤害、格挡、信号、下回合伤害/格挡减半等其他效果与代价。0.5.0按用户要求移除27张牌的消耗，允许重复运转；不因无限构筑额外添加消耗。条件未满足的收益不纳入保证收益；历史回放、能力和他牌触发的收益需看实际场景。','',
    f"相对0.3.0：当回合直接加费牌 {audit['counts']['energyCardsBefore']} → {audit['counts']['energyCardsAfter']}；明确带抽牌步骤的牌 {audit['counts']['drawCardsBefore']} → {audit['counts']['drawCardsAfter']}。另有 {title('RainbowSignal')} 在每回合首次主动消耗信号时立即加费。",'',
    '没有下回合加费、抽牌、信号的正面设计；能力在当前出牌、主动消耗信号或产生透支时触发，偿债时不发奖励。','',
    '## 原生引擎核对','',
    ('下表来自与当前设计SHA-256一致的原生测试，使用本机v0.111.0实现。' if native_current else '设计数值已更改或尚无匹配测试，本次不引用旧版原生测量结果。下方逐牌预算按当前设计重新计算。'),'',
    '| 卡牌 | 升级 | 当回合能量变化（含牌费） | 当前抽牌 | 能量透支 | 抽牌透支 | 净能量 | 净手牌 |',
    '| --- | --- | ---: | ---: | ---: | ---: | ---: | ---: |']
for r in native_rows:
    lines.append(f"| {r['Name']} | {'是' if r['Upgraded'] else '否'} | {r['ImmediateEnergy']} | {r['Drawn']} | {r['EnergyDebt']} | {r['DrawDebt']} | {r['NetEnergy']} | {r['NetDraw']} |")
lines += ['', '原版资源预算可作为调参参照；手动编辑后的强度按当前逐牌预算评估，不沿用旧设计的强度结论。','',
    '伤害/格挡减半及信号流失仍为真实负面代价，未计作正面资源。','',
    '## 改名与模型稳定性','',
    '85张牌按最高谱面定数重新分配歌曲及原曲绘，85图/85曲唯一且逐一对应；类名和模型ID保留。详见ARTWORK.md及content/artwork-map.json。元数据来源：[Phigros_Resource info.tsv](https://raw.githubusercontent.com/7aGiven/Phigros_Resource/info/info.tsv)。','',
    '## 逐牌保守预算','',
    '这里只计无条件效果及自身固定重复次数。消耗信号、前序牌型、出牌数量门槛、额外重复能力等收益未加入，因此并非每张牌的上限；纯伤害牌显示负手牌是出牌成本，不表示它是抽牌牌。','',
    '| 名称 | 升级 | 费用 | 加费 | 抽牌 | 能量透支 | 抽牌透支 | 保证净能量 | 保证净手牌 |','| --- | --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: |']
for r in rows:
    lines.append(f"| {r['name']} | {'是' if r['upgraded'] else '否'} | {r['cost']} | {r['Energy']} | {r['Draw']} | {r['EnergyDebt']} | {r['DrawDebt']} | {r['netEnergy']} | {r['netDraw']} |")
(ROOT/'ECONOMY.md').write_text('\n'.join(lines)+'\n',encoding='utf-8')
print(json.dumps(dict(result='PASS',checks=checks,**audit['counts']),ensure_ascii=True))
