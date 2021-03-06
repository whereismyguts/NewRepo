﻿using System;
using Com.CodeGame.CodeWizards2016.DevKit.CSharpCgdk.Model;
using System.Collections.Generic;
using System.Linq;
using System.Drawing;
using System.Drawing.Imaging;

namespace Com.CodeGame.CodeWizards2016.DevKit.CSharpCgdk {
    public enum State { InBattle, LookFor };
    public enum Problem { Run, Attack, Push, Defend, Bonus };
    internal class AI {
        static Wizard Me; static World World; static Game Game; static Move Move;
        static Grid grid;
        static Vector moveTarget;
        static UnitInfo attackTarget;
        static bool inBattle = false;
        static Problem problem;
        static List<UnitInfo> AllLivingUnits = new List<UnitInfo>(); // must be ordered
        static List<UnitInfo> EnemyUnitsInFight = new List<UnitInfo>(); // must be ordered
        static Vector bonus;

        static int[] skills = { 5, 6, 7, 8, 9, 0, 1, 2, 3, 4 };
        static Vector bot = new Vector(3600, 3600);
        static Vector mid = new Vector(2000, 2000);
        static Vector top = new Vector(400, 400);
        //static int[] skills = {  0, 1, 2, 3, 4 , 5, 6, 7, 8, 9 };
        static int level = 0;

        internal static void MakeMove(Wizard me, World world, Game game, Move move) {
            InitializeTick(me, world, game, move);
            GatherInfo();
            inBattle = InBattle(); //1st level
            problem = CalcProblem(); //2nd level
            CalcTargets(); //3rd level
            ProcessTargets(); //4th level

            LearnSkills();
        }

        static void LearnSkills() {
            if(level != Me.Level && Game.IsSkillsEnabled) {
                level = Me.Level;
                for(int i = 0; i < skills.Length; i++) {
                    if(!Me.Skills.Contains((SkillType)i)) {
                        Move.SkillToLearn = (SkillType)i;
                        return;
                    }
                }
            }
        }

        static Vector prevPosition = new Vector();
        static int stuckCount = 0;
        static void ProcessTargets() {

            if(prevPosition == UnitInfo.MyPosition) {
                stuckCount++;
            }
            else
                stuckCount = 0;
            prevPosition = UnitInfo.MyPosition;

            if(stuckCount > 50)
                SmartWalk(UnitInfo.MyPosition-(moveTarget-UnitInfo.MyPosition));

            if(attackTarget != null) {
                SmartAttack(attackTarget);
                moveTarget = CorrectByPotention(moveTarget);
                SmartWalk(moveTarget);
            }
            else {
                GoSimple(moveTarget);
            }
        }
        static Vector CorrectByPotention(Vector goal) {
            List<UnitInfo> nearBlocks = AllLivingUnits.ToList();

            var creepSpawnCycleTick = World.TickIndex % Game.FactionMinionAppearanceIntervalTicks;
            if(creepSpawnCycleTick > Game.FactionMinionAppearanceIntervalTicks - 100 &&
                creepSpawnCycleTick < Game.FactionMinionAppearanceIntervalTicks)
                nearBlocks.AddRange(UnitInfo.TheirSpawnPoints);

            if(nearBlocks.Count > 0) {
                Vector newDir = (goal - UnitInfo.MyPosition).SetLength(50);
                foreach(UnitInfo unit in nearBlocks)
                    if(unit.Distance <= Me.Radius * 1.5 + unit.Radius) {
                        Vector addForce = UnitInfo.MyPosition - unit.Position;
                        newDir += addForce;
                    }
                return UnitInfo.MyPosition + newDir;
            }
            return goal;
        }
        static void SmartAttack(UnitInfo attackTarget) {
            var action = BestAction();
            if(action != ActionType.Staff) {
                Move.MinCastDistance = attackTarget.Distance - attackTarget.Radius * 1.05;
                Move.MaxCastDistance = attackTarget.Distance + attackTarget.Radius * 1.05;
                Kick(attackTarget, action);
                return;
            }
            else {
                var near = EnemyUnitsInFight.FirstOrDefault(e => e.Distance <= Me.Radius + e.Radius + Game.StaffRange);
                if(near != null) {
                    Kick(near, action);
                    return;
                }
            }
            var tree = AllLivingUnits.Find(u => u.Type == UnitType.Tree && u.Distance <= Me.Radius * 1.1 + u.Radius);// must be ordered;
            if(tree != null)
                Kick(tree, action);
        }
        static void Kick(UnitInfo target, ActionType type) {
            double angle = Me.GetAngleTo(target.Position.X, target.Position.Y);
            if(Math.Abs(angle) <= (type == ActionType.Staff ? Game.StaffSector / 2 : 0.01))
                Move.Action = type;
            else
                Move.Turn = angle;
        }
        static ActionType BestAction() {
            if(Me.Skills.Contains(SkillType.FrostBolt) &&
                 Me.RemainingActionCooldownTicks < 5 &&
                  Me.RemainingCooldownTicksByAction[(int)ActionType.FrostBolt] < 5 &&
                  Me.Mana >= Game.FrostBoltManacost &&
                  attackTarget != null && (attackTarget.Type == UnitType.Wizard || attackTarget.Type == UnitType.Building))
                return ActionType.FrostBolt;
            if(Me.RemainingActionCooldownTicks < 5 && Me.RemainingCooldownTicksByAction[(int)ActionType.MagicMissile] < 5)
                return ActionType.MagicMissile;
            return ActionType.Staff;
        }
        static void SmartWalk(Vector goal) {
            if(goal.IsEmpty)
                return;
            Vector meToGoal = goal - UnitInfo.MyPosition;
            Vector correctSpeed = meToGoal.SetLength(3.0);
            Vector correctDir = correctSpeed.Rotate(-Me.Angle);

            Move.Speed = correctDir.X;
            Move.StrafeSpeed = correctDir.Y;
            //Move.Turn = Me.GetAngleTo(goal.X, goal.Y);
        }
        static void GoSimple(Vector goal) {
            if(grid != null) {
                var path = grid.GetPath(Me.X, Me.Y, moveTarget.X, moveTarget.Y);
                if(path != null && path.Count > 1) {
                    GoStupid(path[1].X, path[1].Y);
                    return;
                }
            }
            GoStupid(goal.X, goal.Y);
        }
        static void GoStupid(double x, double y) {
            //if(WalkAround())
            //    return;

            Vector goal = CorrectByPotention(new Vector(x, y));
            Move.Turn = Me.GetAngleTo(goal.X, goal.Y);
            Move.Speed = Game.WizardForwardSpeed;

            // WalkAround();
        }
        static void CalcTargets() {
            attackTarget = CalcAttackTarget();
            moveTarget = CalcMoveTarget();
            //   DrawOptimalPoint(moveTarget, AllLivingUnits,null,null);

        }
        static UnitInfo CalcAttackTarget() {
            if(EnemyUnitsInFight.Count > 0) {
                var targets = EnemyUnitsInFight.OrderBy(unit => unit.AttackValue);
                return targets.LastOrDefault();
            }
            return null;
            // mist be ordered
            //var tree = AllLivingUnits.Find(u => u.Unit is Tree && u.Distance <= Me.Radius * 1.5 + u.Unit.Radius);// must be ordered
            //return tree;
        }
        static Vector CalcMoveTarget() {
            if(World.TickIndex < 1000 && UnitInfo.MyPosition.DistanceTo(UnitInfo.HomeBase) > 2000)
                return UnitInfo.HomeBase;


            switch(problem) {
                case Problem.Attack:
                    return CalcOptimalLocalPoint(false);
                case Problem.Run:
                    return CalcOptimalLocalPoint(true);
                case Problem.Bonus:
                    return bonus;
                case Problem.Push:
                    return CalcPushPoint();
                case Problem.Defend:
                    return CalcDefendPoint();
            }
            return new Vector(2000, 2000);

        }
        static Vector CalcDefendPoint() {
            var dangerToBase = AllLivingUnits
                .Where(unit => unit.IsEnemy && (unit.Type == UnitType.Wizard || unit.Type == UnitType.Minion))
                .OrderBy(unit => unit.Position.DistanceTo(UnitInfo.HomeBase)).ToList();
            if(dangerToBase != null && dangerToBase.Count > 0)
                return dangerToBase.FirstOrDefault().Position;
            return UnitInfo.HomeBase;
        }
        static Vector CalcPushPoint() {
            //if(Me.Messages.Length > 0) {
            //    try {
            //        int lane = (int)Me.Messages[0].Lane;
            //        if(CalcCurrentLane(UnitInfo.MyPosition) != lane)
            //            return CalcBattlePointInLane(lane);
            //    }
            //    catch(Exception e) {
            //    }
            //}

            var en = EnemyUnitsInFight.FirstOrDefault();
            var next = CalcBattlePointInLane(CalcCurrentLane(UnitInfo.MyPosition));
            if(en != null && en.Distance < next.DistanceTo(UnitInfo.MyPosition))
                return en.Position;
            return next;
        }
        static Vector CalcBattlePointInLane(int lane) {
            var intruders = AllLivingUnits.Where(unit => unit.IsEnemy && CalcCurrentLane(unit.Position) == lane && (unit.Type == UnitType.Wizard || unit.Type == UnitType.Minion)).ToList();
            if(intruders != null && intruders.Count > 0) {
                intruders = intruders.OrderBy(unit => unit.Position.DistanceTo(UnitInfo.HomeBase)).ToList();
                return intruders.First().Position;
            }

            return NextOnLane();
        }
        static int CalcCurrentLane(Vector point) {
            // return (int)LaneType.Top;
            double sum = point.X + point.Y;
            if(sum > 3400 && sum < 4500)
                return (int)LaneType.Middle;
            if(point.X < 500 || point.Y < 500)
                return (int)LaneType.Top;
            if(point.X > 3500 || point.Y > 3500)
                return (int)LaneType.Bottom;
            return -1;
        }
        static Vector NextOnLane() {
            if(UnitInfo.MyPosition.DistanceTo(UnitInfo.HomeBase) > UnitInfo.MyPosition.DistanceTo(UnitInfo.TheirBase))
                return UnitInfo.TheirBase;

            switch(CalcCurrentLane(UnitInfo.MyPosition)) {
                case (int)LaneType.Bottom: return bot;
                case (int)LaneType.Top: return top;
                case (int)LaneType.Middle: return mid;
            }

            return mid;
        }
        static Vector PrevOnLane() {
            if(UnitInfo.MyPosition.DistanceTo(UnitInfo.HomeBase) < UnitInfo.MyPosition.DistanceTo(UnitInfo.TheirBase))
                return UnitInfo.HomeBase;

            switch(CalcCurrentLane(UnitInfo.MyPosition)) {
                case (int)LaneType.Bottom: return bot;
                case (int)LaneType.Top: return top;
                case (int)LaneType.Middle: return mid;
            }

            return UnitInfo.HomeBase;
        }
        static Vector CalcOptimalLocalPoint(bool run) {
            var blocks = AllLivingUnits.Where(u => u.Distance < Me.CastRange * 1.5).ToList(); // must be order
            var enemies = blocks.Where(u => u.IsEnemy).ToList();

            if(enemies.Count == 1 && Me.Life < Me.MaxLife * 0.4) {
                return PrevOnLane();
            }
            var creepSpawnCycleTick = World.TickIndex % Game.FactionMinionAppearanceIntervalTicks;
            if(creepSpawnCycleTick > Game.FactionMinionAppearanceIntervalTicks - 100 &&
                creepSpawnCycleTick < Game.FactionMinionAppearanceIntervalTicks)
                enemies.AddRange(UnitInfo.TheirSpawnPoints);


            var other = blocks.Where(u => !u.IsEnemy);

            //double safeDistance = World.TickIndex > 1000 ? Me.CastRange * 0.3 : Me.CastRange;
            double safeDistance = Me.CastRange * 0.4;
            double maxDistance = Me.CastRange * 0.8;

            double rMax = Me.Radius * 4;
            double rStep = Me.Radius;
            List<Vector> dots = new List<Vector>();
            for(double Rx = -rMax; Rx <= rMax; Rx += rStep)
                for(double Ry = -rMax; Ry <= rMax; Ry += rStep) {

                    Vector dot = new Vector(
                        Me.X + Rx,
                        Me.Y + Ry);



                    if(dot.X < 50 || dot.X > 3950 || dot.Y < 50 || dot.Y > 3950)
                        continue;
                    UnitInfo danger = enemies.FirstOrDefault(u => dot.DistanceTo(u.Position) < (run ? u.GetCastRange() : safeDistance));
                    if(danger != null)
                        continue;
                    UnitInfo block = other.FirstOrDefault(u => dot.DistanceTo(u.Position) < Me.Radius + u.Radius * 1.1);
                    if(block != null)
                        continue;
                    var close = enemies.FirstOrDefault();
                    if(close != null && !run) {
                        if(dot.DistanceTo(close.Position) > maxDistance)
                            continue;
                    }
                    dots.Add(dot);
                }
            if(dots.Count > 0) {
                var runPoint = UnitInfo.HomeBase.DistanceTo(UnitInfo.MyPosition) > Me.CastRange * 2 ? PrevOnLane() : NextOnLane();
                dots = dots.OrderBy(d => d.DistanceTo(runPoint)).ToList();
                return dots[0];
            }

            return PrevOnLane();


        }
        static Problem CalcProblem() {
            //if(UnitInfo.HomeThrone.Life < UnitInfo.HomeThrone.MaxLife / 3)
            // return Problem.Defend;
            if(World.Bonuses.Count() > 0) {
                bonus = new Vector(World.Bonuses[0].X, World.Bonuses[0].Y);
                return Problem.Bonus;
            }
            if(inBattle) {
                return Me.Life < Me.MaxLife * 0.4 ? Problem.Run : Problem.Attack;
            }
            //if(UnitInfo.HomeThrone.Unit.Life < UnitInfo.HomeThrone.Unit.MaxLife / 3)
            //return Problem.Defend;

            var intruders = AllLivingUnits.Where(
                u => u.IsEnemy &&
                u.Position.DistanceTo(UnitInfo.HomeBase) <= 1000 &&
                (u.Type == UnitType.Minion || u.Type == UnitType.Wizard)
                ).ToList();
            if(intruders.Count > 2)
                return Problem.Defend;

            return Problem.Push; // TODo add defend
        }
        static bool InBattle() {
            return EnemyUnitsInFight.Count > 0;
        }
        static void InitializeTick(Wizard me, World world, Game game, Move move) {
            Me = me;
            World = world;
            Game = game;
            Move = move;
            UnitInfo.Me = Me;
            UnitInfo.Game = Game;
            UnitInfo.World = World;
            if(UnitInfo.ShouldInit)
                UnitInfo.SetParams();

            UnitInfo.AttackNeutrals = world.TickIndex < 1000;
        }
        static void GatherInfo() {
            UnitInfo.MyPosition = new Vector(Me.X, Me.Y);
            UpdateMap();
            List<LivingUnit> objects = new List<LivingUnit>(World.Wizards);
            objects.AddRange(World.Minions);
            objects.AddRange(World.Trees);
            objects.AddRange(World.Buildings);

            AllLivingUnits = objects.Where(u => u.Id != Me.Id).Select(o => new UnitInfo(o)).OrderBy(u => u.Distance).ToList();
            EnemyUnitsInFight = AllLivingUnits.Where(u => u.IsEnemy && u.Distance <= Me.VisionRange).OrderBy(u => u.Distance).ToList();
            UnitInfo.RangedDamage = BestAction() == ActionType.FrostBolt ? Game.FrostBoltDirectDamage : Game.MagicMissileDirectDamage;
            UnitInfo.RangedDamage += Game.MagicalDamageBonusPerSkillLevel * Me.Level;
        }
        static void UpdateMap() {
            var objects = new List<CircularUnit>();
            objects.AddRange(World.Buildings);
            objects.AddRange(World.Trees);
            if(grid == null)
                grid = new Grid(objects);
            else
                grid.Reveal(objects);
        }
    }
    public enum UnitType { Minion, Wizard, Building, Tree }
    class UnitInfo {
        public static Wizard Me { get; set; }
        public static Game Game { get; set; }
        LivingUnit Unit { get; set; }
        public double Distance { get; internal set; }
        public static bool ShouldInit { get; set; } = true;
        public static bool AttackNeutrals { get; set; } = false;
        public static Vector HomeBase { get; set; }
        public static Vector TheirBase { get; set; }
        public static Faction They { get; set; }
        public bool IsEnemy {
            get {
                return Unit.Faction == They ||
                  (Unit.Faction == Faction.Neutral &&
                      (AttackNeutrals || Unit.Life < Unit.MaxLife));
            }
        }

        public Vector Position { get; internal set; }
        public static Vector MyPosition { get; internal set; }
        public static UnitInfo HomeThrone { get; internal set; }
        public static World World { get; internal set; }
        public int AttackValue {
            get {
                int res = -(int)Distance;
                if(Unit.Life <= RangedDamage) {
                    res += 100000;
                    return res;
                }
                return res;
                if(Unit is Minion)
                    res += 1000;
                else
                if(Unit is Wizard)
                    res += 3000;
                else
                if(Unit is Building) {
                    res += ((Building)Unit).Type == BuildingType.GuardianTower ? 4000 : 5000;
                }
                return res;
            }
        }
        public static int RangedDamage { get; set; }
        public UnitType Type { get; set; }
        public static List<UnitInfo> TheirSpawnPoints { get; internal set; }
        public double Radius { get; internal set; }
        public int Life { get; internal set; }

        public UnitInfo(LivingUnit unit) {
            Unit = unit;
            Distance = Unit.GetDistanceTo(Me);
            Position = new Vector(unit.X, unit.Y);
            Radius = unit.Radius;
            Life = unit.Life;
            if(Unit is Minion)
                Type = UnitType.Minion;
            else
            if(Unit is Wizard)
                Type = UnitType.Wizard;
            else
            if(Unit is Building)
                Type = UnitType.Building;
            else
                Type = UnitType.Tree;
        }

        public UnitInfo(int x, int y) {
            Position = new Vector(x, y);
            Distance = Position.DistanceTo(MyPosition);
            Radius = 100;
            Type = UnitType.Minion;
            Life = 100;
        }

        internal static void SetParams() {
            HomeBase = new Vector(Me.X, Me.Y);
            TheirBase = new Vector(HomeBase.Y, HomeBase.X);
            // HomeBase = Me.Faction == Faction.Academy ? new Vector(200, 3800) : new Vector(3800, 200);
            //TheirBase = Me.Faction == Faction.Renegades ? new Vector(200, 3800) : new Vector(3800, 200);
            They = Me.Faction == Faction.Academy ? Faction.Renegades : Faction.Academy;
            HomeThrone = new UnitInfo(World.Buildings.FirstOrDefault(b => b.Type == BuildingType.FactionBase && b.Faction == Me.Faction));
            // HomeBase = HomeThrone.Position;
            TheirSpawnPoints = new List<UnitInfo>();

            if(HomeThrone.Position.X < 2000) {
                TheirSpawnPoints.Add(new UnitInfo(3000, 200));
                TheirSpawnPoints.Add(new UnitInfo(3200, 800));
                TheirSpawnPoints.Add(new UnitInfo(3800, 1000));
            }
            else {
                TheirSpawnPoints.Add(new UnitInfo(200, 3000));
                TheirSpawnPoints.Add(new UnitInfo(800, 3200));
                TheirSpawnPoints.Add(new UnitInfo(1000, 3800));
            }

            ShouldInit = false;
        }

        public override string ToString() {
            return Unit.Faction.ToString() + " " + Unit.GetType().Name + ", d:" + Distance;
        }

        public double GetCastRange() {
            if(Game == null) return Me.CastRange;
            switch(Type) {
                case UnitType.Minion:
                    return Game.FetishBlowdartAttackRange;
                case UnitType.Wizard:
                    return ((Wizard)Unit).CastRange;
                case UnitType.Building:
                    return ((Building)Unit).AttackRange + 50;
            }
            return Me.CastRange;
        }
    }
}