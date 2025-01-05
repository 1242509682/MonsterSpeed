# MonsterSpeed 怪物加速插件

- 作者: 羽学
- 出处: Tshock官方群816771079
- 这是一个Tshock服务器插件，主要用于：
- 击中在配置中的“怪物ID”自动生成数据表，使其获得加速能力，根据怪物死亡次数自动减少：冷却时间与触发距离等

## 更新日志

```
v1.0.7
生成弹幕功能加入了【角度】与【弹幕AI】属性
随着弹幕数量增加自动衰减弹幕速度、变化弹幕的发射角度
修正了弹幕生命（持续帧数）的计算方式（不再支持-1写法）

v1.0.6
移除了AI风格
加入了自动转移仇恨机制
将隐藏的【冷却计数】与【更新时间】放入内存中计算,避免配置频繁写入
【时间事件】与【血量事件】加入了【召唤怪物】、【生成弹幕】功能
【监控广播】改为使用全员发送信息，不再遍历玩家是否在线
提高了怪物对玩家的位置判断准确性

v1.0.5
修复时间事件空引用
将时间事件和血量事件从触发距离内移出，仅在触发时间内起效
优化监控广播，只对mos.admin管理权限可见（时间:绿色为触发,玫红色为冷却）,可自定义播报间隔
注意：加速、血量、时间事件共用一个【冷却时间】触发，1次冷却为一个循环周期

v1.0.4
加入超距离追击模式
默认追击距离为50（可通过配置项修改）
当玩家与怪物距离超过追击距离时会以最高速度追击玩家
当追到触发最小距离时自动恢复原始速度
随着玩家击败BOSS次数逐渐减少这个缓冲距离
移除了Y轴加速配置项（做了距离向量算法优化）
预设了全BOSS怪物ID表
【切换智慧】更名为AI风格（可进入官方wiki搜索AI查询）
加入了/mos reset命令，用于清除【怪物数据表】

v1.0.3
将【AI赋值】更名为：血量事件
将【AI循环】更名为：时间事件
并将其格式转换为List数据表
加入了【触发距离外回血】配置项：
当怪物不处于【触发距离】且仍有【触发秒数】时会持续回血（冷却时间与触发加速时不回）
给【血量事件】【时间事件】加入了【切换智慧】配置项（当值为-1时，保持默认）
首次击中怪物可获取到其【切换智慧】值

v1.0.2
隐藏了更新时间与冷却次数配置项
给【ai赋值】加入了血量条件
加入ai监控配置项（此监控仅在触发的有效时间与距离内会全服广播）
考虑到不是所有怪物都具备ai[1]移除默认填写值
(不会写AI循环的可以不写，建议多学习写【血量AI赋值】)
【AI赋值】中第一个字符串是执行修改的血量范围,不再此范围则默认不修改
【AI循环】中第一个数字是执行顺序，再每次冷却结束后按顺序执行，数字内的{}才是ai键值

v1.0.1
加入了伤怪建表法
加入了AI循环：
根据冷却时间自动增加冷却次数，根据冷却次数自动循环AI（移除数组）
根据  "怪物ID"自动填写AI循环中的ai[1] 0.0-3.0的循环值

v1.0.0
羽学自己写的小玩具，
根据角色与怪物之间的距离自动给BOSS实现加速与AI赋值
```

## 指令

| 语法                             | 别名  |       权限       |                   说明                   |
| -------------------------------- | :---: | :--------------: | :--------------------------------------: |
| /mos  | 无 |   mos.admin    |    指令菜单    |
| /mos reset | 无 |   mos.admin    |    重置怪物数据表    |
| /reload  | 无 |   tshock.cfg.reload    |    重载配置文件    |

## 配置
> 配置文件位置：tshock/怪物加速.json
```json
{
  "插件开关": true,
  "触发监控": true,
  "监控间隔": 100.0,
  "默认速度": 12,
  "速度上限": 35,
  "触发秒数上限": 15.0,
  "触发距离上限": 84.0,
  "触发距离外回血": true,
  "默认追击距离": 50.0,
  "击败后加速度": 2,
  "击败后减冷却": 0.5,
  "怪物ID表": [
    4,
    13,
    35,
    50,
    113,
    115,
    125,
    126,
    127,
    134,
    216,
    222,
    243,
    245,
    262,
    266,
    325,
    327,
    344,
    345,
    346,
    370,
    395,
    398,
    439,
    477,
    491,
    541,
    551,
    618,
    620,
    621,
    636,
    657,
    668
  ],
  "怪物数据表": {
    "史莱姆王": {
      "冷却时间": 4.5,
      "自动仇恨": false,
      "追击模式": true,
      "追击距离": 50.0,
      "最低加速": 14,
      "最高加速": 35,
      "触发秒数": 5.5,
      "触发秒数上限": 15.0,
      "触发最小距离": 9.0,
      "触发最大距离": 84.0,
      "血量事件": [
        {
          "最小生命": 50,
          "最大生命": 100,
          "AI赋值": {
            "3": 7200.0
          },
          "召唤怪物": [],
          "召唤数量": 5,
          "生成弹幕": []
        },
        {
          "最小生命": 0,
          "最大生命": 50,
          "AI赋值": {},
          "召唤怪物": [
            184,
            204
          ],
          "召唤数量": 15,
          "生成弹幕": [
            {
              "弹幕ID": 671,
              "数量": 30,
              "伤害": 20,
              "击退": 8,
              "速度": 50.0,
              "持续帧数": 120
            }
          ]
        }
      ],
      "时间事件": [],
      "死亡次数": 1
    },
    "克苏鲁之眼": {
      "冷却时间": 5.0,
      "自动仇恨": false,
      "追击模式": true,
      "追击距离": 50.0,
      "最低加速": 12,
      "最高加速": 35,
      "触发秒数": 5.0,
      "触发秒数上限": 15.0,
      "触发最小距离": 10.0,
      "触发最大距离": 84.0,
      "血量事件": [],
      "时间事件": [
        {
          "顺序": 1,
          "AI赋值": {},
          "召唤怪物": [
            5
          ],
          "召唤数量": 15,
          "生成弹幕": [
            {
              "弹幕ID": 454,
              "数量": 5,
              "伤害": 20,
              "击退": 8,
              "速度": 10.0,
              "持续帧数": 120
            }
          ]
        },
        {
          "顺序": 2,
          "AI赋值": {},
          "召唤怪物": [
            133
          ],
          "召唤数量": 5,
          "生成弹幕": [
            {
              "弹幕ID": 814,
              "数量": 5,
              "伤害": 30,
              "击退": 8,
              "速度": 30.0,
              "持续帧数": 60
            }
          ]
        }
      ],
      "死亡次数": 0
    },
    "世界吞噬怪": {
      "冷却时间": 5.0,
      "自动仇恨": false,
      "追击模式": true,
      "追击距离": 125.0,
      "最低加速": 12,
      "最高加速": 35,
      "触发秒数": 5.0,
      "触发秒数上限": 15.0,
      "触发最小距离": 10.0,
      "触发最大距离": 84.0,
      "血量事件": [],
      "时间事件": [],
      "死亡次数": 0
    },
    "毁灭者": {
      "冷却时间": 5.0,
      "自动仇恨": false,
      "追击模式": true,
      "追击距离": 100.0,
      "最低加速": 12,
      "最高加速": 35,
      "触发秒数": 5.0,
      "触发秒数上限": 15.0,
      "触发最小距离": 10.0,
      "触发最大距离": 84.0,
      "血量事件": [],
      "时间事件": [],
      "死亡次数": 0
    }
  }
}
```
## 反馈
- 优先发issued -> 共同维护的插件库：https://github.com/UnrealMultiple/TShockPlugin
- 次优先：TShock官方群：816771079
- 大概率看不到但是也可以：国内社区trhub.cn ，bbstr.net , tr.monika.love
