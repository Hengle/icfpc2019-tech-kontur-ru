using System;
using System.Linq;
using lib.Solvers.RandomWalk;
using NUnit.Framework;

namespace tests.Solvers
{
    [TestFixture]
    internal class BlockDeepWalkSolverTests : SolverTestsBase
    {
        [Test]
        public void SolveOne()
        {
            var solver = new BlockDeepWalkSolver(50, 2, new Estimator(), usePalka: true);
            SolveOneProblem(solver, 150);
        }
    }
}