
using Com.CodeGame.CodeWizards2016.DevKit.CSharpCgdk.Model;
using System.Linq;
using System.Collections.Generic;
using System;

namespace Com.CodeGame.CodeWizards2016.DevKit.CSharpCgdk {
    public sealed class MyStrategy: IStrategy {

        Wizard me;
        World world;
        Game game;
        Move move;

        int strafe = 0;
        int strafeSpeed = 1;
        //static Grid grid;
        // get target: 
        //correct distance to fave, go away, attack weak enemy, get bomus

        public void Move(Wizard me, World world, Game game, Move move) {
            this.me = me;
            this.world = world;
            this.move = move;
            this.game = game;

            //Path finding test:
            //if(grid == null)
            //    grid = new Grid(world);
            //else
            //    grid.Reveal(world);
            //foreach(Bonus item in world.Bonuses) {
            //    List<Vector> path =  grid.GetPath(new Point((int)me.X, (int)me.Y), new Point((int)item.X, (int)item.Y));
            //}
            //

            //run
            LivingUnit runFrom = FindDanger();
            if(runFrom != null) {
                Goal(false, runFrom.X, runFrom.Y);
                move.Action = ActionType.MagicMissile;
                return;
            }
            //attack
            LivingUnit archEnemy = FindArchEnemy();
            if(archEnemy != null) {
                if(me.GetDistanceTo(archEnemy.X, archEnemy.Y) > me.CastRange * 0.8) {
                    Goal(true, archEnemy.X, archEnemy.Y);
                    return;
                }
                else {
                    Attack(archEnemy);
                    return;
                }
            }
            //strafe = 0; ??
            //find what to do
            FollowMinions();
        }

        private void FollowMinions() {
            LivingUnit fave = GetFave();
            if(fave != null) {
                Vector goal = CalcFaveNearPoint(fave, 0, 100);
                Goal(true, goal.X, goal.Y);
            }
        }

        void Attack(LivingUnit archEnemy) {
            if(Math.Abs(me.GetAngleTo(archEnemy)) > 0.01)
                move.Turn = me.GetAngleTo(archEnemy);
            move.Speed = 0;

            move.Action = ActionType.MagicMissile;
            if(strafe == 30) {
                strafeSpeed = -1;
            }
            if(strafe == -30) {
                strafeSpeed = 1;
            }
            move.StrafeSpeed = strafeSpeed * game.WizardStrafeSpeed;
            strafe += strafeSpeed;
        }

        private LivingUnit GetFave() {
            try {
                return world.Minions.OrderBy(m => m.GetDistanceTo(me)).Last();
            }
            catch { }
            return null;
        }

        LivingUnit FindDanger() {
            LivingUnit danger = GetClosestEnemyUnit();
            if(danger != null && (me.Life < me.MaxLife * 0.5 || me.GetDistanceTo(danger.X, danger.Y) < me.CastRange * 0.4))
                return danger;
            return null;
        }
        LivingUnit GetClosestEnemyUnit() {
            try {
                List<LivingUnit> list = new List<LivingUnit>();
                list.AddRange(world.Minions);
                list.AddRange(world.Wizards);
                list.AddRange(world.Buildings);

                return list
                    .Where(w => w.Faction == me.Faction || w.Faction == Faction.Neutral)
                    .OrderBy(u => u.GetDistanceTo(me))
                    .Last();
            }
            catch { }
            return null;
        }
        private LivingUnit FindArchEnemy() {
            List<LivingUnit> enemiesList = new List<LivingUnit>();
            enemiesList.AddRange(world.Minions);
            enemiesList.AddRange(world.Wizards);
            enemiesList.AddRange(world.Buildings);
            try {
                var enemies = enemiesList.Where(en => (en.Faction != me.Faction && en.Faction != Faction.Neutral));

                // now just find enimy with max value
                double bestValue = double.MinValue;
                LivingUnit result = null;
                foreach(var en in enemies) {
                    double HPfactor = 8.0 - (double)en.Life / en.MaxLife;
                    var dist = en.GetDistanceTo(me);
                    double distFactor = dist >= me.VisionRange ? -10 : dist >= en.Radius + me.Radius ? 1 : dist * 100;
                    double typeFactor = GetTypeFactor(en);

                    double value = (HPfactor + typeFactor + distFactor) / 3.0;
                    if(value > bestValue && value > 0) {
                        bestValue = value;
                        result = en;
                    }
                }
                return result;
            }
            catch { }
            return null;
        }
        double GetTypeFactor(LivingUnit en) {
            if(en is Minion) return 0.3;
            if(en is Wizard) return 1;
            if(en is Building) {
                if((en as Building).Type == BuildingType.FactionBase) return 0.8;
                if((en as Building).Type == BuildingType.GuardianTower) return 0.6;
            }
            return 0;
        }
        Vector CalcFaveNearPoint(Unit goalUnit, double angleTo, double distTo) {
            double angleTotal = goalUnit.Angle + angleTo;
            return new Vector(goalUnit.X - Math.Cos(angleTotal) * distTo, goalUnit.Y - Math.Sin(angleTotal) * distTo);
        }

        void Goal(bool fwd, double x, double y) {
            move.Turn = me.GetAngleTo(x, y);
            move.Speed = fwd ? 30 : -30;
            if(!fwd) move.Action = ActionType.MagicMissile;
            WalkAroundIfNeed();
        }



        private void WalkAroundIfNeed() {
            List<CircularUnit> blocks = new List<CircularUnit>();
            blocks.AddRange(world.Buildings);
            blocks.AddRange(world.Trees);
            blocks.AddRange(world.Minions);
            blocks.AddRange(world.Wizards);

            try {
                CircularUnit obj = blocks.Where(b => b.Id != me.Id).OrderBy(u => u.GetDistanceTo(me)).Last();
                double closeDist = me.Radius + obj.Radius + 10;
                if(obj.GetDistanceTo(me.X, me.Y) < closeDist) {
                    double angle = me.GetAngleTo(obj.X, obj.Y);
                    move.Speed = -Math.Cos(angle) * 3;
                    move.StrafeSpeed = -Math.Sin(angle) * 3;
                    move.Turn = 0;
                }
            }
            catch { };
        }
    }
    public struct Vector {
        public Vector(double x, double y) {
            this.X = x;
            this.Y = y;
        }
        public double X { get; set; }
        public double Y { get; set; }
    }
}