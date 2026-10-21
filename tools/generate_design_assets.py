from pathlib import Path
import json
import re
import shutil
import card_design

ROOT = Path(__file__).resolve().parents[1]

def main():
    root=ROOT
    cards=json.loads((root/'content/cards.json').read_text(encoding='utf-8'))
    rehearsal_name=next(c['name'] for c in cards if c['className']=='GeopeliaRehearsal')
    archive_name=next(c['name'] for c in cards if c['className']=='GeopeliaArchiveLost')
    powers=re.findall(r'public sealed class (Geopelia\w+Power)\s*:',(root/'src/Powers.cs').read_text(encoding='utf-8'))
    def slug(s):return re.sub(r'(?<!^)(?=[A-Z])','_',s).lower()
    pack=root/'build/godot/PhigrosJiu'
    old_icon=pack/'icons/powers/geopelia_combo_power.png'
    for name in powers:
        p=pack/'icons/powers'/f'{slug(name)}.png'
        if not p.exists():shutil.copyfile(old_icon,p)
    registry={'powers':[{'className':p,'entry':'PHIGROS_JIU_'+slug(p).upper(),'icon':slug(p)+'.png'} for p in powers],
              'rarities':card_design.EXPECTED,'signalCap':24}
    (root/'content/mechanics.json').write_text(json.dumps(registry,ensure_ascii=False,indent=2),encoding='utf-8')
    (pack/'mechanics.json').write_text(json.dumps(registry,ensure_ascii=False,indent=2),encoding='utf-8')
    text={}
    for key,(title,body) in card_design.POWER_TEXT.items():text['Geopelia'+key+'Power']=(title,body)
    text.update({
      'GeopeliaSignalPower':('信号','可储存的正面资源，初始上限24点，可由卡牌提高，不自动衰减。卡牌会主动消耗信号以获得收益；透支导致的信号流失不算主动消耗。'),
      'GeopeliaRhythmPower':('随身录音','每回合每打出3张牌，获得1点信号。额外打出与自动重放也计数。'),
      'GeopeliaEnergyDebtPower':('能量透支','下回合能量重置后，失去{Amount}点能量，最低降至0；随后清除此透支。'),
      'GeopeliaDrawDebtPower':('抽牌透支','下回合起始抽牌减少{Amount}张，最低降至0；随后清除此透支。'),
      'GeopeliaSignalDebtPower':('信号透支','下回合开始时，失去至多{Amount}点信号；随后清除此透支。'),
      'GeopeliaDamageDebtPower':('失真预告','下回合牌的伤害减少{Amount}%，持续到该回合结束。同类预告不叠加。'),
      'GeopeliaBlockDebtPower':('失守预告','下回合牌所获得的格挡减少{Amount}%，持续到该回合结束。同类预告不叠加。'),
      'GeopeliaMutedPower':('失真','本回合牌的伤害减少{Amount}%。'),
      'GeopeliaFragileGuardPower':('失守','本回合牌所获得的格挡减少{Amount}%。'),
      'GeopeliaTapeLoopPower':(rehearsal_name+' 回环','本回合接下来手动打出其他牌时，重放 '+rehearsal_name+'。还可触发{Amount}次；回合结束时清除。'),
    })
    for lang in ['zhs','eng']:
        loc={}
        for name in powers:
            title,body=text[name];entry='PHIGROS_JIU_'+slug(name).upper()
            loc[entry+'.title']=title;loc[entry+'.description']=body;loc[entry+'.smartDescription']=body
        (root/f'localization/{lang}/powers.json').write_text(json.dumps(loc,ensure_ascii=False,indent=2),encoding='utf-8')
        rp=root/f'localization/{lang}/relics.json';rel=json.loads(rp.read_text(encoding='utf-8-sig'))
        rel['PHIGROS_JIU_GEOPELIA_JUDGMENT_LINE.title']='随身录音笔'
        rel['PHIGROS_JIU_GEOPELIA_JUDGMENT_LINE.description']='战斗开始时，获得{GeopeliaSignalPower:diff()}点信号。每回合每打出3张牌，获得1点信号。'
        rel['PHIGROS_JIU_GEOPELIA_STARDUST_BADGE.description']='回合结束时，若你有至少4点信号，获得{Block:diff()}点格挡。'
        rp.write_text(json.dumps(rel,ensure_ascii=False,indent=2),encoding='utf-8')
        cp=root/f'localization/{lang}/characters.json';ch=json.loads(cp.read_text(encoding='utf-8-sig'))
        ch['PHIGROS_JIU_GEOPELIA.description']='来自《Phigros》的旅人。\n在重复的节拍中采集信号，以明天为代价演奏此刻。\n\n[font_size=20][color=#b6c0ce]多牌重奏 · 透支未来 · 信号共鸣[/color][/font_size]'
        cp.write_text(json.dumps(ch,ensure_ascii=False,indent=2),encoding='utf-8')
    # Fully specified reference table: one row per real card and both upgrade states.
    labels={'Basic':'基础','Common':'普通','Uncommon':'罕见','Rare':'稀有','Ancient':'先古'}
    lines=['# 鸠 · 卡牌设计','', '主卡池85张：基础4、普通25、罕见29、稀有25、先古2，与沙耶1.8.5 API1110标注主卡池的卡牌数量一致。','',
      '信号是正面储存资源，初始上限24，可由 Evanescent 提高，不自然衰减；主动消耗由牌面规定。起始遗物在战斗开始时给予1点信号，每回合每3次出牌再给予1点。', '',
      '重复打出使用原生重复结算，额外打出也计入出牌数。历史回放只选本回合此前手动打出的非能力、非X费、非回放引擎牌，使用原牌免费自动打出，保留升级、附魔和原生重放次数，不额外添加消耗。'+rehearsal_name+' 只由其他手动牌触发。', '',
      '透支在下回合结算。能量与抽牌透支可累计且最低降至0；伤害、格挡减半的同类透支不叠加。跨战斗不保留透支或信号。','',
      '0.5.0：85首歌曲与85张不同原曲绘逐一对应。最高谱面定数分档：基础12.4–13.4、普通13.6–14.9、罕见15.1–16.2、稀有16.6–17.6、先古17.9–18.0；对应关系见 ARTWORK.md。类名和模型ID保留。','',
      '消耗由30张减为3张，仅先古牌2张及先古牌生成牌1张保留。其余27张恢复可重复打出，不为防止无限另加消耗或调整费用。临时回放复制牌仍在结算后清理，不进入原始牌组。','',
      '能量净收益=当回合加费−牌费−下回合能量透支；抽牌净收益=当回合抽牌−打出本牌的1张−下回合抽牌透支。透支按完整负值记账，不把延期本身算作奖励。详见 ECONOMY.md。','',
      '不再有下回合获得能量/抽牌/信号的正面设计；常驻能力改为在当前回合的出牌、主动消耗信号或产生透支时立即触发。自然偿付透支不产生收益。','',
      '先古牌不进入普通奖励，由稀有牌「'+archive_name+'」生成。建议新开局测试。','',
      '| 稀有度 | 名称 | 费用→升级 | 类型 | 路线 | 效果（基础 / 升级） |','| --- | --- | --- | --- | --- | --- |']
    for c in cards:
        variants=[]
        for upgraded in [False,True]:
            description=card_design.card_text(c,upgraded=upgraded,include_replay=True)
            for i,s in enumerate(c['steps']):
                description=description.replace('{V%d:diff()}'%i,str(s['upgrade'] if upgraded else s['value']))
                description=description.replace('{X%d:diff()}'%i,str(s.get('ux',s.get('x',0)) if upgraded else s.get('x',0)))
            description=re.sub(r'\[/?(?:gold|red)\]','',description).replace('\n','；')
            variants.append(description)
        summary=variants[0] if variants[0]==variants[1] else variants[0]+' / 升级：'+variants[1]
        kws='、'.join({'Exhaust':'消耗','Innate':'固有','Retain':'保留','Ethereal':'虚无','Unplayable':'不能被打出','Sly':'奇巧','Eternal':'永恒'}[k] for k in c.get('keywords',[]))
        upgrade_kws='、'.join({'Exhaust':'消耗','Innate':'固有','Retain':'保留','Ethereal':'虚无','Unplayable':'不能被打出','Sly':'奇巧','Eternal':'永恒'}[k] for k in c.get('upgradeKeywords',[]))
        if upgrade_kws:kws+='（升级新增：'+upgrade_kws+'）'
        lines.append(f'| {labels[c["rarity"]]} | {c["name"]} | {c["cost"]}→{c["upgradeCost"]} | {c["type"]} | {" / ".join(c["themes"])} | {summary} {kws} |')
    (root/'DESIGN.md').write_text('\n'.join(lines),encoding='utf-8')
    print('DESIGN_ASSETS_OK cards=%d powers=%d'%(len(cards),len(powers)))

if __name__=='__main__':main()
