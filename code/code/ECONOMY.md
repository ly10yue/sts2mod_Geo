# 0.5.0 资源净收益核对

目标是略高于原版、接近无色牌的运转强度。透支是完整负值，延期本身不算收益。

能量净收益＝当回合加费−实际牌费−下回合能量透支；手牌净收益＝当回合实际抽牌−打出本牌的1张−下回合抽牌透支。

净值不折算伤害、格挡、信号、下回合伤害/格挡减半等其他效果与代价。0.5.0按用户要求移除27张牌的消耗，允许重复运转；不因无限构筑额外添加消耗。条件未满足的收益不纳入保证收益；历史回放、能力和他牌触发的收益需看实际场景。

相对0.3.0：当回合直接加费牌 3 → 11；明确带抽牌步骤的牌 19 → 40。另有 祈 -我ら神祖と共に歩む者なり- 在每回合首次主动消耗信号时立即加费。

没有下回合加费、抽牌、信号的正面设计；能力在当前出牌、主动消耗信号或产生透支时触发，偿债时不发奖励。

## 原生引擎核对

设计数值已更改或尚无匹配测试，本次不引用旧版原生测量结果。下方逐牌预算按当前设计重新计算。

| 卡牌 | 升级 | 当回合能量变化（含牌费） | 当前抽牌 | 能量透支 | 抽牌透支 | 净能量 | 净手牌 |
| --- | --- | ---: | ---: | ---: | ---: | ---: | ---: |

原版资源预算可作为调参参照；手动编辑后的强度按当前逐牌预算评估，不沿用旧设计的强度结论。

伤害/格挡减半及信号流失仍为真实负面代价，未计作正面资源。

## 改名与模型稳定性

85张牌按最高谱面定数重新分配歌曲及原曲绘，85图/85曲唯一且逐一对应；类名和模型ID保留。详见ARTWORK.md及content/artwork-map.json。元数据来源：[Phigros_Resource info.tsv](https://raw.githubusercontent.com/7aGiven/Phigros_Resource/info/info.tsv)。

## 逐牌保守预算

这里只计无条件效果及自身固定重复次数。消耗信号、前序牌型、出牌数量门槛、额外重复能力等收益未加入，因此并非每张牌的上限；纯伤害牌显示负手牌是出牌成本，不表示它是抽牌牌。

| 名称 | 升级 | 费用 | 加费 | 抽牌 | 能量透支 | 抽牌透支 | 保证净能量 | 保证净手牌 |
| --- | --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| dB doll | 否 | 1 | 0 | 0 | 0 | 0 | -1 | -1 |
| dB doll | 是 | 1 | 0 | 0 | 0 | 0 | -1 | -1 |
| Glaciaxion | 否 | 1 | 0 | 0 | 0 | 0 | -1 | -1 |
| Glaciaxion | 是 | 1 | 0 | 0 | 0 | 0 | -1 | -1 |
| 光 | 否 | 1 | 0 | 1 | 0 | 0 | -1 | 0 |
| 光 | 是 | 1 | 0 | 1 | 0 | 0 | -1 | 0 |
| HumaN | 否 | 1 | 0 | 1 | 0 | 0 | -1 | 0 |
| HumaN | 是 | 1 | 0 | 1 | 0 | 0 | -1 | 0 |
| Eltaw | 否 | 1 | 0 | 0 | 0 | 0 | -1 | -1 |
| Eltaw | 是 | 1 | 0 | 0 | 0 | 0 | -1 | -1 |
| Disorder | 否 | 1 | 0 | 0 | 0 | 0 | -1 | -1 |
| Disorder | 是 | 1 | 0 | 0 | 0 | 0 | -1 | -1 |
| Cereris | 否 | 1 | 0 | 0 | 0 | 0 | -1 | -1 |
| Cereris | 是 | 1 | 0 | 0 | 0 | 0 | -1 | -1 |
| Drop It | 否 | 0 | 0 | 0 | 0 | 0 | 0 | -1 |
| Drop It | 是 | 0 | 0 | 0 | 0 | 0 | 0 | -1 |
| 重生 | 否 | 0 | 0 | 3 | 1 | 0 | -1 | 2 |
| 重生 | 是 | 0 | 0 | 4 | 1 | 0 | -1 | 3 |
| Luminescence | 否 | 1 | 0 | 1 | 0 | 0 | -1 | 0 |
| Luminescence | 是 | 1 | 0 | 1 | 0 | 0 | -1 | 0 |
| Bonus Time | 否 | 1 | 0 | 0 | 0 | 0 | -1 | -1 |
| Bonus Time | 是 | 1 | 0 | 0 | 0 | 0 | -1 | -1 |
| Chronologika | 否 | 1 | 0 | 0 | 0 | 0 | -1 | -1 |
| Chronologika | 是 | 1 | 0 | 0 | 0 | 0 | -1 | -1 |
| DataErr0r | 否 | 0 | 0 | 2 | 0 | 1 | 0 | 0 |
| DataErr0r | 是 | 0 | 0 | 3 | 0 | 1 | 0 | 1 |
| Luminescent | 否 | 1 | 0 | 2 | 0 | 0 | -1 | 1 |
| Luminescent | 是 | 1 | 0 | 2 | 0 | 0 | -1 | 1 |
| ENERGY SYNERGY MATRIX | 否 | 1 | 0 | 2 | 0 | 0 | -1 | 1 |
| ENERGY SYNERGY MATRIX | 是 | 1 | 0 | 3 | 0 | 0 | -1 | 2 |
| Find_Me | 否 | 0 | 0 | 1 | 0 | 0 | 0 | 0 |
| Find_Me | 是 | 0 | 0 | 1 | 0 | 0 | 0 | 0 |
| GOODTEK | 否 | 1 | 0 | 1 | 0 | 0 | -1 | 0 |
| GOODTEK | 是 | 1 | 0 | 1 | 0 | 0 | -1 | 0 |
| Magenta Potion | 否 | 1 | 0 | 0 | 0 | 0 | -1 | -1 |
| Magenta Potion | 是 | 1 | 0 | 0 | 0 | 0 | -1 | -1 |
| Speed Up! | 否 | 0 | 1 | 0 | 1 | 0 | 0 | -1 |
| Speed Up! | 是 | 0 | 1 | 0 | 1 | 0 | 0 | -1 |
| FULi AUTO SHOOTER | 否 | 1 | 0 | 1 | 1 | 0 | -2 | 0 |
| FULi AUTO SHOOTER | 是 | 1 | 0 | 1 | 1 | 0 | -2 | 0 |
| Parallel Retrogression(Game Ver.) | 否 | 1 | 0 | 2 | 0 | 1 | -1 | 0 |
| Parallel Retrogression(Game Ver.) | 是 | 1 | 0 | 3 | 0 | 1 | -1 | 1 |
| Wavetapper | 否 | 1 | 0 | 2 | 0 | 0 | -1 | 1 |
| Wavetapper | 是 | 1 | 0 | 2 | 0 | 0 | -1 | 1 |
| Flutter Echo | 否 | 1 | 0 | 1 | 0 | 0 | -1 | 0 |
| Flutter Echo | 是 | 1 | 0 | 1 | 0 | 0 | -1 | 0 |
| The Whole Rest | 否 | 1 | 1 | 2 | 0 | 0 | 0 | 1 |
| The Whole Rest | 是 | 1 | 1 | 2 | 0 | 0 | 0 | 1 |
| Break Over | 否 | 1 | 0 | 0 | 0 | 0 | -1 | -1 |
| Break Over | 是 | 1 | 0 | 0 | 0 | 0 | -1 | -1 |
| Get Back | 否 | 0 | 0 | 3 | 0 | 1 | 0 | 1 |
| Get Back | 是 | 0 | 0 | 4 | 0 | 1 | 0 | 2 |
| End Me | 否 | 1 | 0 | 0 | 0 | 0 | -1 | -1 |
| End Me | 是 | 1 | 0 | 0 | 0 | 0 | -1 | -1 |
| Unorthodox Thoughts | 否 | 0 | 0 | 0 | 0 | 0 | 0 | -1 |
| Unorthodox Thoughts | 是 | 0 | 0 | 0 | 0 | 0 | 0 | -1 |
| Träne | 否 | 1 | 0 | 0 | 0 | 0 | -1 | -1 |
| Träne | 是 | 1 | 0 | 0 | 0 | 0 | -1 | -1 |
| Bounded Quietude | 否 | 2 | 0 | 0 | 0 | 0 | -2 | -1 |
| Bounded Quietude | 是 | 2 | 0 | 0 | 0 | 0 | -2 | -1 |
| Dance with Silence | 否 | 0 | 0 | 4 | 0 | 0 | 0 | 3 |
| Dance with Silence | 是 | 0 | 0 | 5 | 0 | 0 | 0 | 4 |
| Retribution | 否 | 2 | 0 | 0 | 0 | 0 | -2 | -1 |
| Retribution | 是 | 2 | 0 | 0 | 0 | 0 | -2 | -1 |
| Reimei | 否 | 0 | 0 | 1 | 0 | 0 | 0 | 0 |
| Reimei | 是 | 0 | 0 | 1 | 0 | 0 | 0 | 0 |
| Retribution ~ Cycle of Redemption ~ | 否 | 1 | 0 | 0 | 0 | 0 | -1 | -1 |
| Retribution ~ Cycle of Redemption ~ | 是 | 1 | 0 | 0 | 0 | 0 | -1 | -1 |
| Horizon Blue | 否 | 1 | 0 | 0 | 0 | 0 | -1 | -1 |
| Horizon Blue | 是 | 1 | 0 | 0 | 0 | 0 | -1 | -1 |
| Doppelganger | 否 | 0 | 0 | 0 | 0 | 0 | 0 | -1 |
| Doppelganger | 是 | 0 | 0 | 0 | 0 | 0 | 0 | -1 |
| Nhelv | 否 | 1 | 0 | 0 | 0 | 0 | -1 | -1 |
| Nhelv | 是 | 1 | 0 | 0 | 0 | 0 | -1 | -1 |
| Non-Melodic Ragez (MUG Edit) | 否 | 1 | 0 | 0 | 0 | 0 | -1 | -1 |
| Non-Melodic Ragez (MUG Edit) | 是 | 1 | 0 | 0 | 0 | 0 | -1 | -1 |
| Concvssion | 否 | 1 | 0 | 0 | 0 | 0 | -1 | -1 |
| Concvssion | 是 | 1 | 0 | 0 | 0 | 0 | -1 | -1 |
| Another Me | 否 | 1 | 0 | 0 | 0 | 0 | -1 | -1 |
| Another Me | 是 | 1 | 0 | 0 | 0 | 0 | -1 | -1 |
| opia | 否 | 1 | 0 | 0 | 0 | 0 | -1 | -1 |
| opia | 是 | 1 | 0 | 0 | 0 | 0 | -1 | -1 |
| energy trixxx | 否 | 0 | 2 | 0 | 1 | 0 | 1 | -1 |
| energy trixxx | 是 | 0 | 3 | 0 | 1 | 0 | 2 | -1 |
| El Condor Pasa (Phigros Edit) | 否 | 1 | 0 | 0 | 0 | 0 | -1 | -1 |
| El Condor Pasa (Phigros Edit) | 是 | 1 | 0 | 0 | 0 | 0 | -1 | -1 |
| Re：birth | 否 | 1 | 0 | 2 | 0 | 0 | -1 | 1 |
| Re：birth | 是 | 1 | 0 | 2 | 0 | 0 | -1 | 1 |
| Feast远东之宴 | 否 | 0 | 0 | 3 | 0 | 1 | 0 | 1 |
| Feast远东之宴 | 是 | 0 | 0 | 4 | 0 | 1 | 0 | 2 |
| Labyrinth in Kowloon: Walled World | 否 | 1 | 0 | 0 | 0 | 0 | -1 | -1 |
| Labyrinth in Kowloon: Walled World | 是 | 1 | 0 | 0 | 0 | 0 | -1 | -1 |
| Upshift | 否 | 0 | 0 | 0 | 1 | 0 | -1 | -1 |
| Upshift | 是 | 0 | 0 | 0 | 1 | 0 | -1 | -1 |
| Evanescent | 否 | 1 | 0 | 0 | 0 | 0 | -1 | -1 |
| Evanescent | 是 | 1 | 0 | 0 | 0 | 0 | -1 | -1 |
| Chronomia | 否 | 1 | 0 | 0 | 0 | 0 | -1 | -1 |
| Chronomia | 是 | 1 | 0 | 0 | 0 | 0 | -1 | -1 |
| Radiance | 否 | 0 | 0 | 3 | 0 | 0 | 0 | 2 |
| Radiance | 是 | 0 | 0 | 4 | 0 | 0 | 0 | 3 |
| [NWAD] | 否 | 1 | 2 | 2 | 1 | 0 | 0 | 1 |
| [NWAD] | 是 | 1 | 3 | 3 | 1 | 0 | 1 | 2 |
| NO ONE YES MAN | 否 | 1 | 0 | 1 | 0 | 0 | -1 | 0 |
| NO ONE YES MAN | 是 | 1 | 0 | 1 | 0 | 0 | -1 | 0 |
| GOODFORTUNE | 否 | 2 | 0 | 0 | 0 | 0 | -2 | -1 |
| GOODFORTUNE | 是 | 2 | 0 | 0 | 0 | 0 | -2 | -1 |
| Don't Never Around | 否 | 2 | 0 | 0 | 0 | 0 | -2 | -1 |
| Don't Never Around | 是 | 2 | 0 | 0 | 0 | 0 | -2 | -1 |
| Better Graphic Animation | 否 | 1 | 0 | 0 | 0 | 0 | -1 | -1 |
| Better Graphic Animation | 是 | 1 | 0 | 0 | 0 | 0 | -1 | -1 |
| 青丘 | 否 | 0 | 0 | 2 | 0 | 1 | 0 | 0 |
| 青丘 | 是 | 0 | 0 | 3 | 0 | 1 | 0 | 1 |
| インフェルノシティ | 否 | 2 | 1 | 2 | 0 | 0 | -1 | 1 |
| インフェルノシティ | 是 | 2 | 2 | 3 | 0 | 0 | 0 | 2 |
| Temporal Shifting | 否 | 0 | 0 | 0 | 0 | 0 | 0 | -1 |
| Temporal Shifting | 是 | 0 | 0 | 0 | 0 | 0 | 0 | -1 |
| AbsoluTe disoRdeR | 否 | 3 | 0 | 0 | 0 | 0 | -3 | -1 |
| AbsoluTe disoRdeR | 是 | 2 | 0 | 0 | 0 | 0 | -2 | -1 |
| Rrhar'il | 否 | 2 | 0 | 0 | 0 | 0 | -2 | -1 |
| Rrhar'il | 是 | 1 | 0 | 0 | 0 | 0 | -1 | -1 |
| Entrance to the Chaos | 否 | 0 | 0 | 2 | 0 | 0 | 0 | 1 |
| Entrance to the Chaos | 是 | 0 | 0 | 3 | 0 | 0 | 0 | 2 |
| Petrichor | 否 | 2 | 0 | 0 | 0 | 0 | -2 | -1 |
| Petrichor | 是 | 2 | 0 | 0 | 0 | 0 | -2 | -1 |
| Der Schneid | 否 | 1 | 0 | 2 | 0 | 0 | -1 | 1 |
| Der Schneid | 是 | 1 | 0 | 3 | 0 | 0 | -1 | 2 |
| Distorted Fate | 否 | 0 | 2 | 3 | 1 | 0 | 1 | 2 |
| Distorted Fate | 是 | 0 | 3 | 3 | 1 | 0 | 2 | 2 |
| QZKago Requiem | 否 | 2 | 0 | 0 | 1 | 0 | -3 | -1 |
| QZKago Requiem | 是 | 2 | 0 | 0 | 1 | 0 | -3 | -1 |
| DESTRUCTION 3,2,1 | 否 | 0 | 0 | 3 | 0 | 0 | 0 | 2 |
| DESTRUCTION 3,2,1 | 是 | 0 | 0 | 5 | 0 | 0 | 0 | 4 |
| +ERABY+E CONNEC+10N | 否 | 0 | 0 | 3 | 0 | 0 | 0 | 2 |
| +ERABY+E CONNEC+10N | 是 | 0 | 0 | 4 | 0 | 0 | 0 | 3 |
| 祈 -我ら神祖と共に歩む者なり- | 否 | 1 | 0 | 0 | 0 | 0 | -1 | -1 |
| 祈 -我ら神祖と共に歩む者なり- | 是 | 0 | 0 | 0 | 0 | 0 | 0 | -1 |
| Stardust:RAY | 否 | 1 | 0 | 0 | 1 | 0 | -2 | -1 |
| Stardust:RAY | 是 | 1 | 0 | 0 | 1 | 0 | -2 | -1 |
| Re：End of a Dream | 否 | 2 | 0 | 0 | 0 | 0 | -2 | -1 |
| Re：End of a Dream | 是 | 2 | 0 | 0 | 0 | 0 | -2 | -1 |
| Hydra | 否 | 2 | 0 | 0 | 0 | 0 | -2 | -1 |
| Hydra | 是 | 2 | 0 | 0 | 0 | 0 | -2 | -1 |
| 幻影鬼魅 (PLEASE) | 否 | 0 | 0 | 1 | 0 | 0 | 0 | 0 |
| 幻影鬼魅 (PLEASE) | 是 | 0 | 0 | 1 | 0 | 0 | 0 | 0 |
| 玩具狂奏曲 -終焉- | 否 | 1 | 0 | 1 | 0 | 0 | -1 | 0 |
| 玩具狂奏曲 -終焉- | 是 | 1 | 0 | 2 | 0 | 0 | -1 | 1 |
| Archidoxen | 否 | 2 | 0 | 0 | 0 | 0 | -2 | -1 |
| Archidoxen | 是 | 1 | 0 | 0 | 0 | 0 | -1 | -1 |
| INFiNiTE ENERZY -Overdoze- | 否 | 2 | 0 | 0 | 0 | 0 | -2 | -1 |
| INFiNiTE ENERZY -Overdoze- | 是 | 1 | 0 | 0 | 0 | 0 | -1 | -1 |
| Der Richter | 否 | 2 | 0 | 0 | 0 | 0 | -2 | -1 |
| Der Richter | 是 | 2 | 0 | 0 | 0 | 0 | -2 | -1 |
| sølips | 否 | 1 | 0 | 0 | 0 | 0 | -1 | -1 |
| sølips | 是 | 0 | 0 | 0 | 0 | 0 | 0 | -1 |
| Spasmodic | 否 | 1 | 0 | 0 | 0 | 0 | -1 | -1 |
| Spasmodic | 是 | 1 | 0 | 0 | 0 | 0 | -1 | -1 |
| PRAGMATISM -RESURRECTION- | 否 | 1 | 0 | 0 | 0 | 0 | -1 | -1 |
| PRAGMATISM -RESURRECTION- | 是 | 1 | 0 | 0 | 0 | 0 | -1 | -1 |
| BANGING STRIKE | 否 | 1 | 0 | 0 | 0 | 0 | -1 | -1 |
| BANGING STRIKE | 是 | 0 | 0 | 0 | 0 | 0 | 0 | -1 |
| Igallta | 否 | 2 | 0 | 1 | 0 | 1 | -2 | -1 |
| Igallta | 是 | 2 | 0 | 1 | 0 | 1 | -2 | -1 |
| Stasis | 否 | 0 | 0 | 0 | 0 | 1 | 0 | -2 |
| Stasis | 是 | 0 | 0 | 0 | 0 | 1 | 0 | -2 |
| Cuvism³ | 否 | 2 | 0 | 0 | 0 | 0 | -2 | -1 |
| Cuvism³ | 是 | 2 | 0 | 0 | 0 | 0 | -2 | -1 |
| Desultory Signals | 否 | 2 | 1 | 2 | 0 | 0 | -1 | 1 |
| Desultory Signals | 是 | 2 | 2 | 2 | 0 | 0 | 0 | 1 |
| Exoplanetary Mirage | 否 | 1 | 0 | 0 | 0 | 0 | -1 | -1 |
| Exoplanetary Mirage | 是 | 1 | 0 | 0 | 0 | 0 | -1 | -1 |
| 遗志 | 否 | 0 | 0 | 0 | 0 | 0 | 0 | -1 |
| 遗志 | 是 | 0 | 0 | 0 | 0 | 0 | 0 | -1 |
| 一线生机 | 否 | 0 | 1 | 2 | 0 | 0 | 1 | 1 |
| 一线生机 | 是 | 0 | 1 | 2 | 0 | 0 | 1 | 1 |
| 重启未来 | 否 | 1 | 0 | 0 | 0 | 0 | -1 | -1 |
| 重启未来 | 是 | 0 | 0 | 0 | 0 | 0 | 0 | -1 |
| 割舍 | 否 | 0 | 0 | 0 | 0 | 0 | 0 | -1 |
| 割舍 | 是 | 0 | 0 | 0 | 0 | 0 | 0 | -1 |
