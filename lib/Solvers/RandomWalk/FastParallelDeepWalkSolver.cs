using System;
using System.Collections.Generic;
using System.Linq;
using lib.Models;
using lib.Models.Actions;

namespace lib.Solvers.RandomWalk
{
    public class FastParallelDeepWalkSolver : ISolver
    {
        public string GetName()
        {
            return $"fast-parallel-deep-walk-{depth}-{usePalka}";
        }

        public int GetVersion()
        {
            return 1;
        }

        private readonly int depth;
        private readonly IFastWorkerEstimator estimator;
        private readonly bool usePalka;

        private readonly ActionBase[] availableActions =
        {
            new Rotate(true),
            new Rotate(false),
            new Move("0,1"),
            new Move("0,-1"),
            new Move("1,0"),
            new Move("-1,0")
        };
        private readonly List<List<ActionBase>> chains;

        public FastParallelDeepWalkSolver(int depth, IFastWorkerEstimator estimator, bool usePalka)
        {
            this.depth = depth;
            this.estimator = estimator;
            this.usePalka = usePalka;

            chains = availableActions.Select(x => new List<ActionBase> {x}).ToList();
            for (int i = 1; i < depth; i++)
            {
                chains = chains.SelectMany(c => availableActions.Select(a => c.Concat(new[] {a}).ToList())).ToList();
            }
        }

        public List<List<ActionBase>> Solve(State state)
        {
            var solution = new List<List<ActionBase>> {new List<ActionBase>()};

            if (usePalka)
                BoosterMaster.CreatePalka(state, solution[0]);
            BoosterMaster.CloneAttack(state, solution);

            while (state.UnwrappedLeft > 0)
            {
                // Console.Out.WriteLine($"--BEFORE:\n{state.Print()}");
                
                var partialSolution = new List<List<ActionBase>>();

                while (partialSolution.Count < solution.Count)
                {
                    var part = SolvePart(state, partialSolution);
                    partialSolution.Add(part);
                    //Console.Out.WriteLine($"  PART:\n{part.Format()}");
                }

                for (int i = 0; i < partialSolution[0].Count; i++)
                {
                    for (int j = 0; j < partialSolution.Count; j++)
                        solution[j].Add(partialSolution[j][i]);
                    state.Apply(state.Workers.Select((w, wi) => (w, partialSolution[wi][i])).ToList());
                }

                // if (turn++ > 100)
                //     break;
            }

            return solution;
        }

        public List<ActionBase> SolvePart(State state, List<List<ActionBase>> partialSolution)
        {
            var bestEstimation = double.MinValue;
            List<ActionBase> bestSolution = null;

            foreach (var chain in chains)
            {
                var solution = new List<ActionBase>();
                var undos = new List<Action>();
                for (var c = 0; c < chain.Count; c++)
                {
                    var action = chain[c];
                    if (action is Move moveAction)
                    {
                        var nextPosition = state.Workers[partialSolution.Count].Position + moveAction.Shift;
                        if (!nextPosition.Inside(state.Map) || state.Map[nextPosition] == CellState.Obstacle)
                            break;
                    }

                    undos.Add(state.Apply(
                        state
                            .Workers
                            .Select(
                                (w, i) => (w, i < partialSolution.Count ? partialSolution[i][c]
                                    : i == partialSolution.Count ? action
                                    : new Wait()))
                            .ToList()));
                    solution.Add(action);
                    if (state.UnwrappedLeft == 0)
                        break;
                }

                while (solution.Count < depth)
                {
                    var wait = new Wait();
                    undos.Add(state.Apply(
                        state
                            .Workers
                            .Select((w, i) => (w, i < partialSolution.Count ? partialSolution[i][solution.Count] : wait))
                            .ToList()));
                    solution.Add(wait);
                }

                var estimation = estimator.Estimate(state, state.Workers[partialSolution.Count]);
                //Console.Out.Write($"  w{partialSolution.Count} {estimation} {solution.Format()}");
                if (estimation > bestEstimation)
                {
                    bestEstimation = estimation;
                    bestSolution = solution;
                    //Console.Out.WriteLine(" -- better");
                }
                // else
                //     Console.Out.WriteLine();

                undos.Reverse();
                foreach (var undo in undos)
                    undo();
            }

            return bestSolution;
        }
    }
}