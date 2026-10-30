"""Authoritative card design. Values are base/upgraded, not runtime balance overrides."""
from pathlib import Path
import collections
import json
import re

ROOT = Path(__file__).resolve().parents[1]
CARDS = []

def op(kind, value=0, upgrade=None, **kw):
    return dict(kind=kind, value=value, upgrade=value if upgrade is None else upgrade, **kw)

def card(suffix, name, rarity, cost, type, steps, theme, **kw):
    CARDS.append(dict(className="Geopelia"+suffix, name=name, displayName=kw.pop('displayName',name), rarity=rarity, cost=cost, upgradeCost=kw.pop("upgradeCost",cost), type=type, steps=steps, themes=theme.split("/"), replays=kw.pop("replays",0), **kw))


# Basic
card('Strike', 'dB doll', 'Basic', 1, 'Attack', [op('Attack', 7, 10)], '基础', displayName='打击')
card('Defend', 'Glaciaxion', 'Basic', 1, 'Skill', [op('Block', 6, 9)], '基础', displayName='防御')
card('OpeningBeat', '光', 'Basic', 1, 'Skill', [op('Block', 6, 9), op('Signal', 2, 3), op('Draw', 1, 1)], '信号/多牌', displayName='光')
card('Counterbeat', 'HumaN', 'Basic', 1, 'Attack', [op('AttackBonus', 5, 6, x=2, ux=3, condition='signal', threshold=1, pay=1), op('Draw', 1, 1, condition='replay')], '多牌/信号', replays=1, displayName='人')

# Common
card('Eltaw', 'Eltaw', 'Common', 1, 'Attack', [op('Attack', 7, 9)], '多牌', replays=1, displayName='回声')
card('IMustSayNo', 'Disorder', 'Common', 1, 'Skill', [op('Block', 10, 13), op('Weak', 1, 2)], '基础', displayName='失序')
card('Cereris', 'Cereris', 'Common', 1, 'Attack', [op('AllAttack', 11, 14), op('Signal', 1, 2)], '信号', displayName='远星')
card('Reimei', 'Drop It', 'Common', 0, 'Attack', [op('Attack', 7, 11), op('Signal', 1, 1)], '信号', displayName='碟')
card('Rebirth', '重生', 'Common', 0, 'Skill', [op('Draw', 3, 4), op('EnergyDebt', 1, 1)], '透支/多牌', keywords=['Exhaust'], displayName='重生')
card('Luminescence', 'Luminescence', 'Common', 1, 'Attack', [op('Attack', 7, 9), op('Signal', 1, 1), op('Draw', 1, 1, condition='replay')], '多牌/信号', replays=1, displayName='冷光')
card('Tap', 'Bonus Time', 'Common', 1, 'Attack', [op('Attack', 12, 16), op('Draw', 3, 3, condition='plays', threshold=2)], '多牌', displayName='奖励时间！')
card('Hold', 'Chronologika', 'Common', 1, 'Skill', [op('Block', 10, 13), op('Signal', 1, 1, condition='plays', threshold=2)], '多牌/信号', displayName='时序逻辑')
card('Flick', 'DataErr0r', 'Common', 0, 'Skill', [op('Signal', 2, 3), op('Draw', 2, 3), op('DrawDebt', 1, 1)], '透支/信号', displayName='病毒')
card('Drag', 'Luminescent', 'Common', 1, 'Attack', [op('Attack', 8, 11), op('Draw', 2, 2)], '多牌', displayName='流光')
card('Sync', 'ENERGY SYNERGY MATRIX', 'Common', 1, 'Skill', [op('Signal', 8, 12)], '信号/多牌', displayName='能量矩阵')
card('Probe', 'Find_Me', 'Common', 0, 'Skill', [op('Signal', 2, 3), op('Draw', 1, 1)], '信号', displayName='自我')
card('Carrier', 'GOODTEK', 'Common', 1, 'Attack', [op('AttackBonus', 8, 10, x=6, ux=8, condition='signal', threshold=2, pay=2), op('Draw', 1, 1)], '信号/多牌', displayName='良锤')
card('Envelope', 'Magenta Potion', 'Common', 1, 'Skill', [op('BlockBonus', 8, 11, x=8, ux=11, condition='signal', threshold=2, pay=2)], '信号', displayName='洋红药水')
card('Advance', 'Speed Up!', 'Common', 0, 'Skill', [op('Energy', 1, 1), op('EnergyDebt', 1, 1), op('Signal', 3, 4)], '透支/多牌', keywords=['Retain', 'Exhaust'], displayName='加速')
card('Rush', 'FULi AUTO SHOOTER', 'Common', 1, 'Attack', [op('Attack', 18, 20), op('Draw', 1, 2), op('EnergyDebt', 1, 1)], '透支', displayName='自动射击')
card('BorrowShield', 'Parallel Retrogression(Game Ver.)', 'Common', 1, 'Skill', [op('Block', 14, 18), op('DrawDebt', 1, 1)], '透支/多牌', displayName='平行；逆行')
card('EchoNeedle', 'Wavetapper', 'Common', 1, 'Attack', [op('Attack', 4, 5), op('Draw', 1, 1, condition='replay')], '多牌', replays=2, displayName='编织波纹')
card('ReplayFootnote', 'Flutter Echo', 'Common', 1, 'Skill', [op('Block', 6, 7), op('Signal', 2, 2, condition='replay')], '多牌/信号', replays=1, displayName='颤动回声')
card('Rest', 'The Whole Rest', 'Common', 1, 'Skill', [op('RareNonPower', 1, 1)], '多牌', upgradeCost=0, displayName='休止符')
card('Redline', 'Break Over', 'Common', 1, 'Attack', [op('AttackBonus', 9, 9, x=4, ux=6, condition='perPlay')], '多牌', displayName='突破界限')
card('Scavenge', 'Get Back', 'Common', 0, 'Skill', [op('BlockBonus', 8, 8, x=3, ux=5, condition='perPlay')], '多牌', displayName='归来')
card('DelayBeat', 'End Me', 'Common', 1, 'Attack', [op('Attack', 17, 22), op('BlockDebt', 50, 50)], '透支', displayName='我之终点')
card('Lowpass', 'Unorthodox Thoughts', 'Common', 0, 'Skill', [op('Block', 6, 8), op('Weak', 1, 2)], '基础', displayName='异端思绪')
card('Scanline', 'Träne', 'Common', 1, 'Attack', [op('AllAttack', 12, 14), op('Draw', 2, 2, condition='plays', threshold=2)], '多牌', displayName='泪')

# Uncommon
card('DistortedFate', 'Bounded Quietude', 'Uncommon', 2, 'Attack', [op('Attack', 36, 42), op('BlockDebt', 50, 50)], '透支', displayName='静谧边界')
card('DanceWithSilence', 'Dance with Silence', 'Uncommon', 0, 'Skill', [op('AllWeak', 2, 4), op('AllVulnerable', 2, 4), op('DamageDebt', 33, 33)], '透支/多牌', displayName='寂静之舞')
card('TerabyteConnection','Retribution','Uncommon',2,'Attack',[op('Attack',8,10),op('Signal',1,1)],'多牌/信号',replays=1, displayName='报应')
card('DiamondEyes', 'Reimei', 'Uncommon', 0, 'Skill', [op('Signal', 3, 3), op('Block', 6, 8)], '信号/多牌', displayName='黎明')
card('Retribution', 'Retribution ~ Cycle of Redemption ~', 'Uncommon', 1, 'Attack', [op('Attack', 9, 12), op('Draw', 1, 1, condition='replay')], '多牌/信号', signalReplayCost=3, displayName='轮回救赎')
card('BelieveLight', 'Horizon Blue', 'Uncommon', 2, 'Power', [op('Power', 6, 5, power='Aerial')], '多牌', displayName='蓝色地平')
card('DoubleExposure', 'Doppelganger', 'Uncommon', 1, 'Skill', [op('Power', 1, 1, power='NextRepeat')], '多牌', displayName='镜中分身', upgradeCost=0)
card('Counterpoint', 'Nhelv', 'Uncommon', 1, 'Skill', [op('SignalStrength', 1, 1)], '信号', keywords=['Exhaust'], upgradeKeywords=['Retain'], displayName='神经节')
card('Polyphony', 'Non-Melodic Ragez (MUG Edit)', 'Uncommon', 1, 'Power', [op('Power', 4, 5, power='PulseGuard')], '多牌', displayName='无调怒潮')
card('ChannelMux', 'Concvssion', 'Uncommon', 1, 'Skill', [op('Block', 8, 11), op('Draw', 3, 3, pay=2)], '信号/多牌', displayName='震荡')
card('SignalBurst', 'Another Me', 'Uncommon', 1, 'Attack', [op('SignalAttack', 9, 9, x=3, ux=5, cap=4)], '信号', displayName='另一个我')
card('ShieldArray','opia','Uncommon',1,'Skill',[op('SignalRare',4,3,cap=12)],'信号', displayName='视界')
card('BorrowedLight', 'energy trixxx', 'Uncommon', 0, 'Skill', [op('Energy', 3, 4), op('EnergyDebt', 1, 1)], '透支/多牌', displayName='能量戏法')
card('Deadline','El Condor Pasa (Phigros Edit)','Uncommon',1,'Attack',[op('DebtAttack',9,11)],'透支/多牌', displayName='山鹰之歌')
card('Recharge', 'Re：birth', 'Uncommon', 1, 'Skill', [op('Block', 9, 12), op('RepayEnergy', 1, 1), op('Draw', 1, 2)], '透支/多牌', displayName='诞生')
card('Refinance','Feast远东之宴','Uncommon',0,'Skill',[op('RepayEnergy',2,3),op('Draw',3,4),op('DrawDebt',1,1)],'透支/多牌', displayName='远东之宴')
card('Interest', 'Labyrinth in Kowloon: Walled World', 'Uncommon', 1, 'Power', [op('Power', 4, 6, power='OverdriveGuard')], '透支', displayName='迷宫城')
card('Overspeed', 'Upshift', 'Uncommon', 0, 'Attack', [op('Attack', 14, 18), op('EnergyDebt', 1, 1)], '透支/多牌', displayName='升级')
card('Afterglow','Evanescent','Uncommon',1,'Power',[op('Power',12,18,power='SignalCapacity')],'信号', displayName='渐逝')
card('TimeSignature', 'Chronomia', 'Uncommon', 1, 'Attack', [op('Attack', 9, 12), op('Draw', 2, 3, condition='lastSkill'), op('Energy', 1, 1, condition='replay')], '多牌', displayName='天文钟')
card('GraceNote','Radiance','Uncommon',1,'Skill',[op('ExhaustDrawReplay',1,2)],'多牌/消耗', displayName='辉光')
card('ColdStart', '[NWAD]', 'Uncommon', 1, 'Skill', [op('Energy', 2, 3), op('Signal', 3, 4), op('Draw', 2, 3), op('EnergyDebt', 2, 2)], '透支/信号', displayName='无昼无夜')
card('HandOff', 'NO ONE YES MAN', 'Uncommon', 1, 'Attack', [op('Attack', 13, 17), op('Energy', 1, 1, pay=2)], '信号/多牌', displayName='无人应许')
card('Chorus', 'GOODFORTUNE', 'Uncommon', 2, 'Attack', [op('AllAttack', 18, 22), op('AllWeak', 2, 3, pay=2)], '信号', displayName='良幸')
card('Cadence', "Don't Never Around", 'Uncommon', 2, 'Attack', [op('AttackBonus', 7, 7, x=4, ux=6, condition='signal', threshold=1, pay=1)], '多牌/信号', replays=2, displayName='循环')
card('Rehearsal', 'Better Graphic Animation', 'Uncommon', 1, 'Skill', [op('Block', 2, 3), op('EchoSelf', 99, 99)], '多牌', displayName='更好的动画')
card('ReplayBuffer','青丘','Uncommon',0,'Skill',[op('Replay',1,1),op('Draw',2,3),op('DrawDebt',1,1)],'多牌/透支', displayName='青丘')
card('CarrierWave', 'インフェルノシティ', 'Uncommon', 2, 'Skill', [op('Signal', 6, 8), op('Block', 12, 15), op('Draw', 2, 3)], '信号/多牌', displayName='炼狱都市')
card('PhaseShift', 'Temporal Shifting', 'Uncommon', 0, 'Skill', [op('Energy', 1, 1, condition='debt'), op('Block', 5, 8)], '透支/多牌', displayName='时相迁移')

# Rare
card('AbsoluteDisorder','AbsoluTe disoRdeR','Rare',2,'Attack',[op('FatalRewardAttack',10,14)],'斩杀',keywords=['Exhaust'], displayName='绝对混乱')
card('Feast', "Rrhar'il", 'Rare', 2, 'Power', [op('Power', 2, 1, power='SignalEcho')], '信号/多牌', upgradeCost=1, displayName='猎犬')
card('ChristmasCarol', 'Entrance to the Chaos', 'Rare', 1, 'Skill', [op('Draw', 2, 3), op('Replay', 1, 1)], '多牌', displayName='混沌入口', upgradeCost=1)
card('CrushBeta', 'Petrichor', 'Rare', 2, 'Attack', [op('Attack', 45, 55), op('DamageDebt', 50, 50)], '透支/多牌', keywords=['Eternal'], displayName='救赎')
card('HappyDisease', 'Der Schneid', 'Rare', 1, 'Skill', [op('EndlessSignal', 1, 1)], '多牌', keywords=['Exhaust'], upgradeCost=0, displayName='勇气')
card('MomentarySunrise', 'Distorted Fate', 'Rare', 0, 'Skill', [op('Energy', 2, 3), op('Draw', 3, 3), op('EnergyDebt', 1, 1)], '透支/多牌', keywords=['Exhaust'], displayName='扭曲命运')
card('OverdraftSuite', 'QZKago Requiem', 'Rare', 1, 'Attack', [op('SignalExecute', 3, 4)], '信号/斩杀', displayName='安魂曲')
card('TimeMortgage', 'DESTRUCTION 3,2,1', 'Rare', 0, 'Skill', [op('Draw', 3, 5), op('DamageDebt', 30, 30), op('BlockDebt', 30, 30)], '透支/多牌', displayName='毁灭倒数')
card('DebtClearing', '+ERABY+E CONNEC+10N', 'Rare', 0, 'Skill', [op('RepayEnergy', 99, 99), op('RepayDraw', 99, 99)], '透支/多牌', keywords=['Exhaust'], upgradeKeywords=['Retain'], upgradeCost=0, displayName='星兆连接')
card('RainbowSignal', '祈 -我ら神祖と共に歩む者なり-', 'Rare', 1, 'Power', [op('Power', 1, 1, power='Beacon')], '信号/多牌', upgradeCost=0, displayName='神明共行者')
card('PhantomEnsemble','Stardust:RAY','Rare',1,'Skill',[op('Replay',2,3),op('EnergyDebt',1,1)],'多牌/透支', displayName='星尘之光')
card('FullPlayback', 'Re：End of a Dream', 'Rare', 2, 'Skill', [op('Replay', 3, 4)], '多牌', displayName='终梦')
card('LastTransmission', 'Hydra', 'Rare', 2, 'Attack', [op('SignalAttack', 0, 0, x=4, ux=5, cap=10)], '多牌/信号', replays=1, displayName='九首蛇')
card('SignalSingularity', '幻影鬼魅 (PLEASE)', 'Rare', 1, 'Skill', [op('Power', 1, 1, power='FutureRestart')], '回合', keywords=['Exhaust'], displayName='幻影鬼魅')
card('NoiseGate', '玩具狂奏曲 -終焉-', 'Rare', 1, 'Skill', [op('Block', 11, 14), op('RepayEnergy', 1, 1, pay=2), op('RepayDraw', 2, 2, pay=2)], '信号/透支', displayName='玩具狂奏曲')
card('SustainAmplifier','Archidoxen','Rare',2,'Power',[op('Power',1,1,power='Archidoxen')],'多牌',upgradeCost=1, displayName='秘奥之书')
card('Tachycardia', 'INFiNiTE ENERZY -Overdoze-', 'Rare', 2, 'Power', [op('Power', 1, 1, power='DebtDraw')], '透支/多牌', upgradeCost=1, displayName='无尽超量')
card('OpeningTheme', 'Der Richter', 'Rare', 2, 'Power', [op('Strength', 4, 6), op('Dexterity', 4, 6), op('Power', 1, 1, power='Richter')], '多牌', displayName='法官')
card('SafetyCopy', 'sølips', 'Rare', 1, 'Skill', [op('Block', 10, 11), op('Replay', 1, 1)], '多牌', upgradeCost=0, displayName='太阳')
card('ContinuousTake', 'Spasmodic', 'Rare', 0, 'Attack', [op('HandSignalAttack', 1, 1), op('DamageDebt', 100, 50), op('BlockDebt', 100, 50)], '信号/消耗', costsX=True, displayName='痉挛')
card('Mastering', 'PRAGMATISM -RESURRECTION-', 'Rare', 1, 'Power', [op('Power', 3, 4, power='EchoSignal')], '信号/多牌', displayName='复生')
card('ArchiveLost', 'BANGING STRIKE', 'Rare', 1, 'Skill', [op('Ancient', 1, 2)], '信号/多牌', keywords=['Exhaust'], upgradeCost=1, displayName='超级打击')
card('MonochromeBreak', 'Igallta', 'Rare', 2, 'Attack', [op('AllAttack', 25, 30), op('AllVulnerable', 2, 3, pay=2), op('Draw', 1, 1), op('DrawDebt', 1, 1)], '信号/透支', displayName='方舟')
card('QuietCoda', 'Stasis', 'Rare', 1, 'Skill', [op('AddReplay', 2, 3)], '多牌', displayName='船')
card('WorldEcho', 'Cuvism³', 'Rare', 4, 'Power', [op('Power', 2, 2, power='WorldEcho')], '多牌/信号', upgradeCost=2, displayName='立方')

card('Hate', 'ハテ', 'Rare', 3, 'Power', [op('Power', 1, 1, power='Hate')], '伤害/多牌', upgradeCost=2, displayName='幽蓝形态')

# Ancient
card('OldRecording', 'Desultory Signals', 'Ancient', 2, 'Skill', [op('Signal', 2, 2), op('Draw', 2, 2), op('AllAttack', 2, 2), op('Block', 2, 2), op('AllVulnerable', 2, 2), op('AllWeak', 2, 2), op('RepayEnergy', 2, 2), op('RepayDraw', 2, 2), op('Energy', 1, 2), op('Strength', 2, 2), op('Dexterity', 2, 2), op('Replay', 0, 1)], '信号', keywords=['Innate', 'Retain', 'Eternal', 'Exhaust', 'Sly'], upgradeCost=2, signalReplayCost=12, descriptionOverride='反击的决意', displayName='断续“信号”')
card('TowerResonance', 'Exoplanetary Mirage', 'Ancient', 1, 'Attack', [op('Attack', 9, 14), op('Signal', 1, 1)], '多牌/信号', replays=4, keywords=['Ethereal'], displayName='系外幻境')

EXPECTED = {'Basic':4,'Common':25,'Uncommon':29,'Rare':26,'Ancient':2}
assert dict(collections.Counter(c['rarity'] for c in CARDS)) == EXPECTED
assert len({c['className'] for c in CARDS}) == sum(EXPECTED.values())
assert len({c['name'] for c in CARDS}) == sum(EXPECTED.values())

POWER_TEXT = {
 'FutureRestart':('幻影鬼魅 (PLEASE)','下个回合开始时，强制结束你的回合，并跳过紧接着的敌方回合。'),
 'Aerial':('Horizon Blue','每打出{Amount}张牌，[gold]重放[/gold]一次最后打出的牌。重放的牌也计入其中，计数跨回合保留。'),
 'Hate':('HATE','回合开始时，你造成和受到的伤害翻倍。打出{Amount}张牌后退出该状态，并令下次退出所需的出牌数增加1。'),
 'NextRepeat':('二重曝光','本回合接下来{Amount}张手动打出的攻击牌重放1。'),
 'PulseGuard':('复调','每打出3张牌，获得{Amount}点格挡。'),
 'OverdriveGuard':('借拍利息','每次打出一张带透支代价的牌，获得{Amount}点格挡。每次结算最多触发一次。'),
 'EchoGuard':('余像防线','重放的牌结算后，获得{Amount}点格挡。'),
 'SignalEcho':('全频共鸣','每回合第一张有足够信号的手动攻击牌，消耗{Amount}点信号，重放1。'),
 'RhythmDraw':('开心病','每打出3张牌，抽{Amount}张牌。'),
 'Beacon':('Colorful Days♪','每回合首次主动消耗信号后，立即获得{Amount}点能量。'),
 'SignalDraw':('Infinity Heaven','每消耗3点信号，抽{Amount}张牌。'),
 'DebtDraw':('HumaN','带透支代价的牌结算后，立即抽{Amount}张牌。'),
 'OpeningSignal':('开场白','每回合前{Amount}张手动打出的牌，各获得1点信号。'),
 'EchoSignal':('母带刻录','重放的牌结算后，获得1点信号。'),
 'WorldEcho':('世界的回响','每回合前{Amount}张可重放的手动非能力牌，各消耗2点信号并重放1。信号不足时不触发。'),
 'Archidoxen':('Archidoxen','记录你打出的上一张牌。打出牌时，与其费用相同：获得{Amount}点能量；稀有度相同：抽{Amount}张牌；类型相同：下一次加费或抽牌翻倍。每个效果每回合最多触发一次。'),
 'Richter':('Der Richter','回合开始时，失去{Amount}点能量。'),
 'SignalCapacity':('Evanescent','信号上限增加{Amount}点。'),
}

def normalize_step(source):
    s=dict(source)
    s.setdefault('value',0);s.setdefault('upgrade',s['value'])
    bonus=s['kind'] in ('AttackBonus','BlockBonus') or (s['kind'] in ('SignalAttack','SignalBlock') and s.get('cap',0)>0)
    if s['value']==0 and s['upgrade']==0 and not (bonus and (s.get('x',0)>0 or s.get('ux',s.get('x',0))>0)):
        return None
    if s['kind'] in ('AttackBonus','BlockBonus') and s.get('x',0)==0 and s.get('ux',s.get('x',0))==0:
        s['kind']='Attack' if s['kind']=='AttackBonus' else 'Block'
        for key in ('x','ux','condition','threshold','pay'):s.pop(key,None)
    if s.get('condition')=='plays' and s.get('threshold',0)==0:s.pop('condition',None)
    for key in ('pay','cap','threshold'):
        if s.get(key)==0:s.pop(key,None)
    if s.get('x',0)==0 and s.get('ux',0)==0:
        s.pop('x',None);s.pop('ux',None)
    return s


def step_text(s, index, chinese=True, upgraded=None, power_card=False):
    # Keep native dynamic variables, while choosing which clauses exist in each upgrade state.
    if upgraded is None:
        normal=step_text(s,index,chinese,False,power_card);up=step_text(s,index,chinese,True,power_card)
        return normal if normal==up else '{IfUpgraded:show:'+up+'|'+normal+'}'
    v='{V%d:diff()}'%index; x='{X%d:diff()}'%index
    amount=s['upgrade'] if upgraded else s['value']
    bonus=s.get('ux',s.get('x',0)) if upgraded else s.get('x',0)
    if amount<=0 and not (s['kind'] in ('AttackBonus','BlockBonus','SignalAttack','SignalBlock') and bonus>0):return ''
    templates = {
      'Attack':f'造成{v}点伤害。','AllAttack':f'对所有敌人造成{v}点伤害。','Block':f'获得{v}点格挡。',
      'Draw':f'抽{v}张牌。','Energy':f'获得{v}点能量。','Signal':f'获得{v}点[gold]信号[/gold]。',
      'EnergyDebt':f'[red]透支[/red]：下回合失去{v}点能量。','DrawDebt':f'[red]透支[/red]：下回合少抽{v}张牌。',
      'SignalDebt':f'[red]透支[/red]：下回合失去至多{v}点信号。','DamageDebt':f'[red]透支[/red]：下回合牌的伤害减少{v}%。同类透支不叠加。',
      'BlockDebt':f'[red]透支[/red]：下回合牌获得的格挡减少{v}%。同类透支不叠加。',
      'Weak':f'给予目标{v}层虚弱。','AllWeak':f'给予所有敌人{v}层虚弱。',
      'AllVulnerable':f'给予所有敌人{v}层易伤。',
      'SignalAttack':((f'消耗至多{s.get("cap",0)}点信号。' if s.get('cap',0)>0 and bonus>0 else '')+(f'造成{v}点伤害。' if amount>0 else '')+(f'每消耗1点信号，额外造成{x}点伤害。' if s.get('cap',0)>0 and bonus>0 else '')),
      'SignalBlock':((f'消耗至多{s.get("cap",0)}点信号。' if s.get('cap',0)>0 and bonus>0 else '')+(f'获得{v}点格挡。' if amount>0 else '')+(f'每消耗1点信号，额外获得{x}点格挡。' if s.get('cap',0)>0 and bonus>0 else '')),
      'RepayEnergy':('清除下回合全部能量透支。' if amount==99 else f'减少{v}点下回合的能量透支。'),
      'RepayDraw':('清除下回合全部抽牌透支。' if amount==99 else f'减少{v}张下回合的抽牌透支。'),
      'SignalStrength':'消耗所有[gold]信号[/gold]，获得等量[gold]力量[/gold]。',
      'SignalRare':f'消耗至多{s.get("cap",0)}点[gold]信号[/gold]。每消耗{v}点，将一张随机的鸠的稀有牌加入手牌。',
      'RareNonPower':f'将{v}张随机的稀有非能力牌加入手牌。',
      'EndlessSignal':f'[gold]不息的信号[/gold]：下个回合开始时，将{v}张随机技能牌加入手牌，其可免费打出，并获得[gold]消耗[/gold]与[gold]不息的信号[/gold]。',
      'DebtAttack':f'本场战斗中，你每打出过一张带[red]透支[/red]的牌，造成{v}点伤害。',
      'ExhaustDrawReplay':f'消耗一张手牌，抽{v}张牌。若消耗的是状态牌或诅咒牌，免费重放此牌。',
      'SignalExecute':f'若目标生命低于[gold]信号[/gold]×{v}，消耗所有信号，将其斩杀。',
      'FatalRewardAttack':f'造成{v}点伤害。[gold]斩杀[/gold]：本场战斗结算中的卡牌奖励稀有度提高一级（稀有牌不变）。',
      'HandSignalAttack':'消耗所有能量、[gold]信号[/gold]和所有手牌。对所有敌人造成三者消耗量乘积的伤害。',
      'Strength':f'获得{v}点[gold]力量[/gold]。','Dexterity':f'获得{v}点[gold]敏捷[/gold]。',
      'Replay':f'依次免费重放本回合此前手动打出的最后{v}张可重放的非能力牌。',
      'AddReplay':f'选择一张已有[gold]重放[/gold]的手牌，使其本场战斗的[gold]重放[/gold]次数增加{v}。',
      'EchoSelf':f'本回合接下来每手动打出一张其他牌，自动重放此牌，最多{v}次。',
      'Ancient':f'将{v}张随机的鸠的先古牌加入手牌。',
    }
    if s['kind']=='Power':
        name, text = POWER_TEXT[s['power']]
        return ('' if power_card or s['power']=='FutureRestart' else f'获得[gold]{name}[/gold]：')+text.replace('{Amount}',v)
    if s['kind'] in ['AttackBonus','BlockBonus']:
        verb='造成' if s['kind']=='AttackBonus' else '获得'; noun='伤害' if s['kind']=='AttackBonus' else '格挡'
        text=f'{verb}{v}点{noun}。' if amount>0 else ''
        if bonus<=0:return text
        if s.get('condition')=='perPlay':return text+f'本回合内此前你每打出一张牌，其{noun}增加{x}点。'
        payment=f'消耗{s["pay"]}点信号并' if s.get('pay',0)>0 else ''
        if s.get('condition')=='signal':text+=(f'若有至少{s["threshold"]}点信号，' if s.get('threshold',0)>0 else '')+payment+f'额外{verb}{x}点{noun}。'
        else:text+=f'若有待偿付的透支，'+payment+f'额外{verb}{x}点{noun}。'
        return text
    text=templates[s['kind']]
    if s.get('pay',0):text=f'消耗{s["pay"]}点信号：'+text
    condition=s.get('condition')
    prefix={'plays':(f'若本回合此前已打出至少{s.get("threshold",0)}张牌，' if s.get('threshold',0)>0 else ''),'replay':'若此次为重放，','lastAttack':'若本回合此前最后打出的是攻击牌，','lastSkill':'若本回合此前最后打出的是技能牌，','debt':'若有待偿付的透支，'}.get(condition,'')
    return prefix+text


def card_text(c, upgraded=None, include_replay=False):
    if c.get('descriptionOverride'):return c['descriptionOverride']
    parts=[]
    if include_replay and c.get('replays',0):parts.append(f'[gold]重放{c["replays"]}[/gold]。')
    if c.get('signalReplayCost',0):parts.append(f'若有至少{c["signalReplayCost"]}点信号，消耗这些信号并[gold]重放1[/gold]。')
    if c.get('playedReplayThreshold',0):parts.append(f'若本回合此前已打出至少{c["playedReplayThreshold"]}张牌，[gold]重放1[/gold]。')
    parts += [text for i,s in enumerate(c['steps']) if (text:=step_text(s,i,upgraded=upgraded,power_card=c['type']=='Power'))]
    return '\n'.join(parts)

def main():
    artwork={c['className']:c for c in json.loads((ROOT/'content/artwork-map.json').read_text(encoding='utf-8'))['cards']}
    for c in CARDS:
        art=artwork[c['className']]
        assert c['name']==art['name'] and c['rarity']==art['rarity'], 'Authoring title and artwork manifest differ'
        for s in c['steps']:
            if s['kind']=='Power':POWER_TEXT[s['power']]=(c['displayName'],POWER_TEXT[s['power']][1])
    song_rows=[row.split('\t') for row in (ROOT/'research/info.tsv').read_text(encoding='utf-8-sig').splitlines() if '\t' in row]
    songs={}
    for row in song_rows:songs.setdefault(row[1].strip(),row)
    assert all(c['name'] in songs for c in CARDS), 'A card title is not an exact Phigros song title'
    assert not any(s['kind']=='NextEnergy' for c in CARDS for s in c['steps']), 'Delayed positive effects are forbidden'
    assert not any('回合开始' in body and ('获得' in body or '抽' in body) for _,body in POWER_TEXT.values()), 'Turn-start benefits are forbidden'
    for i,c in enumerate(CARDS):
        c['steps']=[s for raw in c['steps'] if (s:=normalize_step(raw)) is not None]
        art=artwork[c['className']]
        c['portrait']=art['portrait']
        c['entry']='PHIGROS_JIU_'+re.sub(r'(?<!^)(?=[A-Z])','_',c['className']).upper()
        c['songId']=art['songId']
        c['chartConstant']=art['constant']
        c['artSourceSha256']=art['sha256']
        c['keywords']=c.get('keywords',[])
        # Native BaseReplay text is appended by CardModel itself, not duplicated in localization.
        c['description']=card_text(c)
    text=json.dumps(CARDS,ensure_ascii=False,indent=2)
    (ROOT/'content/cards.json').write_text(text,encoding='utf-8')
    song_map=[dict(className=c['className'],name=c['name'],displayName=c['displayName'],songId=c['songId'],composer=songs[c['name']][2]) for c in CARDS]
    (ROOT/'content/song-name-map.json').write_text(json.dumps({'source':'https://raw.githubusercontent.com/7aGiven/Phigros_Resource/info/info.tsv','cards':song_map},ensure_ascii=False,indent=2),encoding='utf-8')
    json_cs='''// Generated by tools/card_design.py. Edit the design source, not this file.
namespace PhigrosJiu;
internal static class DesignJson { public const string Value = """
'''+text+'\n"""; }\n'
    (ROOT/'src/DesignCatalog.g.cs').write_text(json_cs,encoding='utf-8')
    declarations=['// Generated playable card models.','namespace PhigrosJiu;']
    for c in CARDS:declarations.append(f'public sealed class {c["className"]} : GeopeliaCard {{ public {c["className"]}() : base(Design.Get(nameof({c["className"]}))) {{ }} }}')
    (ROOT/'src/Cards/DesignedCards.g.cs').write_text('\n'.join(declarations),encoding='utf-8')
    for lang in ['zhs','eng']:
        loc={}
        for c in CARDS:
            # Keep fully specified text as the fallback, rather than missing gameplay descriptions.
            loc[c['entry']+'.title']=c['displayName'] if lang=='zhs' else c['name']
            loc[c['entry']+'.description']=c['description']
            if any(s['kind']=='AddReplay' for s in c['steps']):
                loc[c['entry']+'.selectionScreenPrompt']='选择一张已有重放的手牌。'
        (ROOT/f'localization/{lang}/cards.json').write_text(json.dumps(loc,ensure_ascii=False,indent=2),encoding='utf-8')
    audit={'sample':'figure_Saya 1.8.5 API1110','expected':EXPECTED,'actual':dict(collections.Counter(c['rarity'] for c in CARDS)),'themes':dict(collections.Counter(t for c in CARDS for t in c['themes']))}
    (ROOT/'content/design-audit.json').write_text(json.dumps(audit,ensure_ascii=False,indent=2),encoding='utf-8')
    print(json.dumps(audit,ensure_ascii=True))

if __name__=='__main__':main()
