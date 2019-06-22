using System;
using System.Collections.Generic;
using System.Linq;
using lib.Models.Actions;

namespace lib.Models
{
    public class State
    {
        public State(Worker worker, Map map, List<Booster> boosters)
        {
            Workers = new List<Worker> {worker};
            Map = map;
            Boosters = boosters;
            Time = 0;
            ExtensionCount = 0;
            FastWheelsCount = 0;
            DrillCount = 0;
            TeleportCount = 0;
            CloningCount = 0;
            Wrap();
            UnwrappedLeft = Map.VoidCount();
        }

        public Worker SingleWorker => Workers.Single();

        public List<Worker> Workers { get; private set; }
        public Map Map { get; private set; }
        public int UnwrappedLeft { get; set; }
        public List<Booster> Boosters { get; private set; }
        public int Time { get; private set; }

        public int ExtensionCount { get; set; }
        public int FastWheelsCount { get; set; }
        public int DrillCount { get; set; }
        public int TeleportCount { get; set; }
        public int CloningCount { get; set; }

        public Action Apply(IReadOnlyList<(Worker worker, ActionBase action)> workerActions)
        {
            if (workerActions.Count != Workers.Count)
                throw new InvalidOperationException("workerActions.Count != Workers.Count");

            var actions = Workers.Select(w => workerActions.Single(x => x.worker == w).action).ToList();
            
            var prevWorkers = Workers.Select(x => x.Clone()).ToList();
            var undos = actions.Select((x, i) => x.Apply(this, Workers[i])).ToList();
            undos.Reverse();
            Workers.ForEach(x => x.NextTurn());
            Time++;
            return () =>
            {
                Time--;
                Workers = prevWorkers;
                undos.ForEach(u => u());
            };
        }

        public Action Apply(ActionBase action)
        {
            return Apply(new[] {(SingleWorker, action)});
        }

        public Action ApplyRange(IEnumerable<ActionBase> actions)
        {
            var undos = new List<Action>();
            foreach (var action in actions)
                undos.Add(Apply(action));
            undos.Reverse();
            return () =>
            {
                foreach (var action in undos)
                    action();
            };
        }

        public Action Wrap()
        {
            var res = new List<(V pos, CellState oldState)>();
            foreach (var worker in Workers)
            {
                WrapPoint(worker.Position);
                foreach (var manipulator in worker.Manipulators)
                {
                    var p = worker.Position + manipulator;
                    if (p.Inside(Map) && Map.IsReachable(p, worker.Position))
                        WrapPoint(p);
                }
            }

            void WrapPoint(V pp)
            {
                res.Add((pp, Map[pp]));
                if (Map[pp] == CellState.Void)
                    UnwrappedLeft--;
                Map[pp] = CellState.Wrapped;
            }

            return () => Unwrap(res);
        }

        public State Clone()
        {
            var clone = (State)MemberwiseClone();
            clone.Map = clone.Map.Clone();
            clone.Workers = clone.Workers.Select(x => x.Clone()).ToList();
            clone.Boosters = clone.Boosters.ToList();
            return clone;
        }

        public Action CollectBoosters()
        {
            var boostersToCollect = Boosters
                .Where(b => Workers.Any(w => b.Position == w.Position) && b.Type != BoosterType.MysteriousPoint)
                .ToList();
            foreach (var booster in boostersToCollect)
            {
                switch (booster.Type)
                {
                    case BoosterType.Extension:
                        ExtensionCount++;
                        break;
                    case BoosterType.FastWheels:
                        FastWheelsCount++;
                        break;
                    case BoosterType.Drill:
                        DrillCount++;
                        break;
                    case BoosterType.Teleport:
                        TeleportCount++;
                        break;
                    case BoosterType.Cloning:
                        CloningCount++;
                        break;
                }

                Boosters.Remove(booster);
            }

            return () =>
            {
                foreach (var booster in boostersToCollect)
                {
                    switch (booster.Type)
                    {
                        case BoosterType.Extension:
                            ExtensionCount--;
                            break;
                        case BoosterType.FastWheels:
                            FastWheelsCount--;
                            break;
                        case BoosterType.Drill:
                            DrillCount--;
                            break;
                        case BoosterType.Teleport:
                            TeleportCount--;
                            break;
                        case BoosterType.Cloning:
                            CloningCount--;
                            break;
                    }
                }

                Boosters.AddRange(boostersToCollect);
            };
        }

        public void Unwrap(List<(V pos, CellState oldState)> wrappedCells)
        {
            foreach (var wrappedCell in wrappedCells)
            {
                Map[wrappedCell.pos] = wrappedCell.oldState;
                if (wrappedCell.oldState == CellState.Void)
                    UnwrappedLeft++;
            }
        }
    }
}