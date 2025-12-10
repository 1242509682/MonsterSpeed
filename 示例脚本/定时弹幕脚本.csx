using System;
using Terraria;
using Microsoft.Xna.Framework;
using TShockAPI;

// ======================
// 定时弹幕脚本
// 设计：
// 1. 10秒大循环
// 2. 阶段A：第2-9秒，每2.5秒执行一次
// 3. 阶段B：第9-10秒，每0.3秒执行一次（执行3次后触发阶段C）
// 4. 阶段C：阻断B 0.45秒，执行两次
// 5. 5秒小循环：独立并行，旋转后释放弹幕
// ======================

if (Npc == null) return "NPC为空";
if (State == null) return "State为空";
if (Msg == null) return "Msg为空";

// 获取脚本循环计数
int loop = 0;
if (State.ScriptLoop.ContainsKey(State.EventIndex))
{
    loop = State.ScriptLoop[State.EventIndex];
}
else
{
    State.ScriptLoop[State.EventIndex] = 0;
    loop = 0;
}

// 初始化指示物
if (!State.Has("bigLoop")) State.Set("bigLoop", 0);
if (!State.Has("smallLoop")) State.Set("smallLoop", 0);
if (!State.Has("phaseA_cnt")) State.Set("phaseA_cnt", 0);
if (!State.Has("phaseB_cnt")) State.Set("phaseB_cnt", 0);
if (!State.Has("phaseC_cnt")) State.Set("phaseC_cnt", 0);
if (!State.Has("phaseBlock")) State.Set("phaseBlock", 0);
if (!State.Has("rotateAng")) State.Set("rotateAng", 0);

// 帧率假设（60帧/秒）
const int FRAME_RATE = 60;

// ========== 10秒大循环 ==========
int bigLoop = State.Get("bigLoop");
bigLoop++;

// 10秒循环（600帧）
if (bigLoop >= 10 * FRAME_RATE)
{
    bigLoop = 0;
    State.Set("phaseA_cnt", 0);
    State.Set("phaseB_cnt", 0);
    State.Set("phaseC_cnt", 0);
    State.Set("phaseBlock", 0);
    TSPlayer.All.SendMessage($"[c/00FF00:{Npc.FullName}] 大循环重置", 0, 255, 0);
}

State.Set("bigLoop", bigLoop);

// 转换为秒
float bigTime = bigLoop / (float)FRAME_RATE;

// ========== 5秒小循环 ==========
int smallLoop = State.Get("smallLoop");
smallLoop++;

// 5秒循环（300帧）
if (smallLoop >= 5 * FRAME_RATE)
{
    smallLoop = 0;
    State.Set("rotateAng", 0);
}

State.Set("smallLoop", smallLoop);
float smallTime = smallLoop / (float)FRAME_RATE;

// ========== 阶段判断 ==========
string phaseMsg = "";
bool execPhaseA = false;
bool execPhaseB = false;
bool execPhaseC = false;

// 阶段A：第2-9秒，每2.5秒执行一次
if (bigTime >= 2f && bigTime <= 9f)
{
    int phaseA_frame = (int)(2.5f * FRAME_RATE);
    int phaseA_mod = bigLoop % phaseA_frame;
    
    if (phaseA_mod == 0)
    {
        execPhaseA = true;
        int phaseA_cnt = State.Get("phaseA_cnt") + 1;
        State.Set("phaseA_cnt", phaseA_cnt);
        phaseMsg = $"阶段A-{phaseA_cnt}";
    }
}

// 阶段B：第9-10秒，每0.3秒执行一次
bool isBlocked = State.Get("phaseBlock") > 0;
if (!isBlocked && bigTime >= 9f && bigTime <= 10f)
{
    int phaseB_frame = (int)(0.3f * FRAME_RATE);
    int phaseB_mod = bigLoop % phaseB_frame;
    
    if (phaseB_mod == 0)
    {
        int phaseB_cnt = State.Get("phaseB_cnt") + 1;
        State.Set("phaseB_cnt", phaseB_cnt);
        
        // 执行3次后触发阶段C
        if (phaseB_cnt <= 3)
        {
            execPhaseB = true;
            phaseMsg = $"阶段B-{phaseB_cnt}";
            
            // 第3次时标记触发阶段C
            if (phaseB_cnt == 3)
            {
                State.Set("phaseBlock", 1);
                State.Set("phaseC_cnt", 0);
                TSPlayer.All.SendMessage($"[c/FF5500:{Npc.FullName}] 阶段C触发！", 255, 85, 0);
            }
        }
    }
}

// 阶段C：阻断B 0.45秒，执行两次
if (State.Get("phaseBlock") > 0)
{
    int blockFrame = (int)(0.45f * FRAME_RATE);
    int phaseC_cnt = State.Get("phaseC_cnt");
    
    // 第一次执行
    if (phaseC_cnt == 0)
    {
        execPhaseC = true;
        phaseMsg = "阶段C-1";
        State.Set("phaseC_cnt", 1);
    }
    // 第二次执行（0.45秒后）
    else if (phaseC_cnt == 1 && State.Get("phaseBlock") >= blockFrame)
    {
        execPhaseC = true;
        phaseMsg = "阶段C-2";
        State.Set("phaseC_cnt", 2);
        State.Set("phaseBlock", 0);
        State.Set("phaseB_cnt", 0);
        TSPlayer.All.SendMessage($"[c/00AAFF:{Npc.FullName}] 阶段C完成，恢复阶段B", 0, 170, 255);
    }
    
    if (execPhaseC)
    {
        // 更新阻断计数
        State.Set("phaseBlock", State.Get("phaseBlock") + 1);
    }
}

// ========== 阶段执行 ==========
var plr = GetTar();
if (plr == null || !plr.active || plr.dead)
{
    phaseMsg += " (目标无效)";
}
else
{
    var npcPos = Npc.Center;
    var tarPos = plr.Center;
    var dir = tarPos - npcPos;
    
    if (dir == Vector2.Zero) dir = new Vector2(1, 0);
    dir.Normalize();
    
    float baseAng = (float)Math.Atan2(dir.Y, dir.X);
    
    // 阶段A：强力弹幕
    if (execPhaseA)
    {
        // A1：直线弹幕
        for (int i = 0; i < 5; i++)
        {
            var vel = dir.RotatedBy(MathHelper.ToRadians(-20 + i * 10)) * 12f;
            SpawnProj(113, 30, vel, 150);
        }
        
        // A2：圆形弹幕
        int circleCnt = 8;
        for (int i = 0; i < circleCnt; i++)
        {
            float ang = MathHelper.TwoPi / circleCnt * i;
            var circleVel = new Vector2((float)Math.Cos(ang), (float)Math.Sin(ang)) * 8f;
            SpawnProj(115, 25, circleVel, 180);
        }
        
        TSPlayer.All.SendMessage($"[c/FFFF00:{Npc.FullName}] 阶段A执行: 直线+圆形弹幕", 255, 255, 0);
    }
    
    // 阶段B：快速弹幕
    if (execPhaseB)
    {
        // B1：快速散射
        int quickCnt = 12;
        for (int i = 0; i < quickCnt; i++)
        {
            float ang = baseAng + MathHelper.ToRadians(-30 + i * 5);
            var quickVel = new Vector2((float)Math.Cos(ang), (float)Math.Sin(ang)) * 15f;
            SpawnProj(664, 18, quickVel, 120);
        }
        
        // B2：追踪弹幕
        var homingVel = dir * 8f;
        int homingProj = Projectile.NewProjectile(
            Npc.GetSpawnSourceForNPCFromNPCAI(),
            npcPos, homingVel, 440, 35, 6f, Main.myPlayer, 0f, 1f);
        if (homingProj >= 0)
        {
            Main.projectile[homingProj].timeLeft = 240;
            Main.projectile[homingProj].friendly = false;
        }
        
        TSPlayer.All.SendMessage($"[c/FFAA00:{Npc.FullName}] 阶段B执行: 快速散射+追踪", 255, 170, 0);
    }
    
    // 阶段C：强力爆发
    if (execPhaseC)
    {
        // C1：爆炸弹幕
        var explosionPos = tarPos;
        int explosionProj = Projectile.NewProjectile(
            Npc.GetSpawnSourceForNPCFromNPCAI(),
            explosionPos, Vector2.Zero, 134, 60, 12f, Main.myPlayer);
        if (explosionProj >= 0)
        {
            Main.projectile[explosionProj].timeLeft = 60;
            Main.projectile[explosionProj].friendly = false;

        }
        
        // C2：环绕弹幕
        int surroundCnt = 16;
        float surroundRad = 4f;
        float surroundSpd = 6f;
        
        for (int i = 0; i < surroundCnt; i++)
        {
            float ang = MathHelper.TwoPi / surroundCnt * i;
            var pos = npcPos + new Vector2((float)Math.Cos(ang), (float)Math.Sin(ang)) * surroundRad * 16f;
            var surroundVel = new Vector2((float)Math.Cos(ang), (float)Math.Sin(ang)) * surroundSpd;
            SpawnProj(671, 28, surroundVel, 200);
        }
        
        // C3：BOSS回血
        HealNpc(100);
        
        TSPlayer.All.SendMessage($"[c/FF00FF:{Npc.FullName}] 阶段C执行: 爆炸+环绕+回血", 255, 0, 255);
    }
}

// ========== 5秒小循环：旋转后释放 ==========
if (plr != null && plr.active && !plr.dead)
{
    var npcPos = Npc.Center;
    var tarPos = plr.Center;
    var dir = tarPos - npcPos;
    
    if (dir == Vector2.Zero) dir = new Vector2(1, 0);
    dir.Normalize();
    
    float baseAng = (float)Math.Atan2(dir.Y, dir.X);
    
    // 小循环阶段
    float smallTimeMod = smallTime % 5f;
    
    // 前2.5秒：旋转
    if (smallTimeMod <= 2.5f)
    {
        // 更新旋转角度
        int rotateAng = State.Get("rotateAng") + 1;
        State.Set("rotateAng", rotateAng);
        
        // 显示旋转特效
        float rotRad = 3f;
        int rotCnt = 6;
        
        for (int i = 0; i < rotCnt; i++)
        {
            float ang = MathHelper.TwoPi / rotCnt * i + MathHelper.ToRadians(rotateAng);
            var pos = npcPos + new Vector2((float)Math.Cos(ang), (float)Math.Sin(ang)) * rotRad * 16f;
            
            // 创建旋转粒子效果（使用小伤害弹幕）
            int particle = Projectile.NewProjectile(
                Npc.GetSpawnSourceForNPCFromNPCAI(),
                pos, Vector2.Zero, 90, 5, 2f, Main.myPlayer);
            if (particle >= 0)
            {
                Main.projectile[particle].timeLeft = 10;
                Main.projectile[particle].friendly = false;
            }
        }
        
        if (smallLoop % 30 == 0) // 每0.5秒显示一次
        {
            TSPlayer.All.SendMessage($"[c/55AAFF:{Npc.FullName}] 小循环: 旋转蓄力...", 85, 170, 255);
        }
    }
    // 后2.5秒：释放
    else
    {
        // 每0.5秒释放一次
        if (smallLoop % 30 == 0)
        {
            // 向玩家方向释放弹幕
            float releaseAng = baseAng + MathHelper.ToRadians(State.Get("rotateAng"));
            var releaseVel = new Vector2((float)Math.Cos(releaseAng), (float)Math.Sin(releaseAng)) * 20f;
            
            SpawnProj(115, 40, releaseVel, 180);
            TSPlayer.All.SendMessage($"[c/FF5555:{Npc.FullName}] 小循环: 释放旋转弹幕！", 255, 85, 85);
        }
    }
}

// ========== 状态显示 ==========
if (loop % 30 == 0) // 每0.5秒更新一次
{
    string bigPhase = "";
    if (bigTime >= 2f && bigTime <= 9f) bigPhase = "阶段A";
    else if (bigTime >= 9f && bigTime <= 10f)
    {
        if (State.Get("phaseBlock") > 0) bigPhase = "阶段C";
        else bigPhase = "阶段B";
    }
    else bigPhase = "空闲";
    
    string smallPhase = (smallTime % 5f <= 2.5f) ? "旋转" : "释放";
    
    Msg.Clear();
    Msg.AppendLine($"=== 定时弹幕脚本 ===");
    Msg.AppendLine($"大循环: {bigTime:F1}s/{10f}s ({bigPhase})");
    Msg.AppendLine($"小循环: {smallTime:F1}s/{5f}s ({smallPhase})");
    Msg.AppendLine($"A次数: {State.Get("phaseA_cnt")}, B次数: {State.Get("phaseB_cnt")}");
    Msg.AppendLine($"阻断: {State.Get("phaseBlock")}, 旋转角: {State.Get("rotateAng")}");
    
    if (!string.IsNullOrEmpty(phaseMsg))
    {
        Msg.AppendLine($"当前: {phaseMsg}");
    }
}

return $"循环: {loop}, 时间: {bigTime:F1}s";