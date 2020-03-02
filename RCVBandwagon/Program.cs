using System;
using System.Collections.Generic;
using System.Linq;

namespace RCVBandwagon
{
    class Program
    {
        // Candidates: We assume a fixed number of candidates for the election.
        // Voters: We assume a fixed number of voters for the election.
        // Dimensions: Each candidate has a fixed location with respect to each dimension. A dimension may be variable (something like ideology, meaning that different voters may prefer different values) or fixed (something like experience, where all voters are assumed to prefer an extreme value). 
        // Weighting scheme: Each dimension has some weight in the calculus, with earlier dimensions tending to have more weight than later dimensions.

        // Puzzle: With one dimension, candidates in middle 


        static Random Randomizer = new Random(0);

        static int numRepetitions = 25_000;
        static bool reportEach = false;
        static int numCandidates = 10;
        static int numDimensions = 2; 
        static int numVoters = 1000; 
        static double probabilityBunching = 0; // if great than zero, most of weight of subsequent candidate is based on earlier candidate
        static double weightBunching = 0.75;
        static double probabilityDimensionVariable = 1.0;
        static bool eachDimensionEqualWeight = true;
        static double maxProportionRemainingWeight = 0.5; // if not equal weight for each dimension, this determines the maximum proportion of the unallocated weight should be assigned to each dimension (other than the last, which receives the remaining weight) -- this approach means that there will usually be some dimensions with greater weight than others
        static double distancePower = 1.0; // exponent for measuring distance
        static double polarizationWeight = 0.0; // greater weight pushes voters preferred attributes more toward extremes
        static bool doBandwagon = true;
        static int numCandidatesInBandwagon = 2;
        static double bandwagonProportion => doBandwagon ? 0.5 : 0; // proportion of voters with bandwagon effect
        static double bandwagonEffect = 3.0; // for voter with bandwagon effect, top two candidates have scores decreased by this many standard deviations of score

        public class Dimension
        {
            public bool VotersHaveVariablePreferences;
            public double Weight;
        }
        public Dimension[] Dimensions;

        public class Candidate
        {
            public double[] Attributes;
        }
        public Candidate[] Candidates;

        public class Voter
        {
            public double[] PreferredAttributes;
            public double[] CandidateScores;
        }

        public Voter[] Voters;

        public void RepeatedExecution()
        {
            for (double bandwagonEffectValue = 0; bandwagonEffectValue < 5.0; bandwagonEffectValue += 0.5)
            {
                bandwagonEffect = bandwagonEffectValue;
                double trueCondorcetWinnerExists = 0;
                double revealedCondorcetWinnerExists = 0;
                double rankedChoiceWinnerIsTrue = 0;
                double rankedChoiceWinnerIsRevealed = 0;
                double revealedCondorcetWinnerIsTrue = 0;
                for (int r = 0; r < numRepetitions; r++)
                {
                    var result = ExecuteOnce();
                    if (result.trueCondorcetWinnerExists)
                        trueCondorcetWinnerExists++;
                    if (result.revealedCondorcetWinnerExists)
                        revealedCondorcetWinnerExists++;
                    if (result.rankedChoiceWinnerIsTrue)
                        rankedChoiceWinnerIsTrue++;
                    if (result.rankedChoiceWinnerIsRevealed)
                        rankedChoiceWinnerIsRevealed++;
                    if (result.revealedCondorcetWinnerIsTrue)
                        revealedCondorcetWinnerIsTrue++;
                    
                }
                //Console.WriteLine($"Bandwagon effect {bandwagonEffectValue} True Condorcet winner {trueCondorcetWinnerExists / (double)numRepetitions} Revealed Condorcet winner {revealedCondorcetWinnerExists / (double)numRepetitions} Ranked choice is true {rankedChoiceWinnerIsTrue / trueCondorcetWinnerExists} Ranked choice is revealed {rankedChoiceWinnerIsRevealed / revealedCondorcetWinnerExists }");
                Console.WriteLine($"{bandwagonEffectValue: 0.00}, {trueCondorcetWinnerExists / (double)numRepetitions: 0.00}, {revealedCondorcetWinnerExists / (double)numRepetitions : 0.00}, {rankedChoiceWinnerIsTrue / trueCondorcetWinnerExists: 0.00}, {rankedChoiceWinnerIsRevealed / revealedCondorcetWinnerExists: 0.00}, {revealedCondorcetWinnerIsTrue / trueCondorcetWinnerExists: 0.00}");
            }
        }

        public (bool trueCondorcetWinnerExists, bool revealedCondorcetWinnerExists, bool rankedChoiceWinnerIsTrue, bool rankedChoiceWinnerIsRevealed, bool revealedCondorcetWinnerIsTrue) ExecuteOnce()
        {
            Setup();
            bool originalDoBandwagon = doBandwagon;
            doBandwagon = false;
            DoAssessments();
            int? trueCondorcetWinner = CondorcetWinner();
            doBandwagon = originalDoBandwagon;
            DoAssessments();
            int? revealedCondorcetWinner = CondorcetWinner();
            int rankedChoiceWinner = RankedChoiceWinner();

            bool trueCondorcetWinnerExists = trueCondorcetWinner != null;
            bool revealedCondorcetWinnerExists = revealedCondorcetWinner != null;
            bool rankedChoiceWinnerIsTrue = trueCondorcetWinnerExists && rankedChoiceWinner == trueCondorcetWinner;
            bool rankedChoiceWinnerIsRevealed = revealedCondorcetWinnerExists && rankedChoiceWinner == revealedCondorcetWinner;
            bool revealedCondorcetWinnerIsTrue = trueCondorcetWinnerExists && trueCondorcetWinnerExists == revealedCondorcetWinnerExists;

            if (reportEach)
            {
                Console.WriteLine(GetCandidateAttributes(0));
                Console.WriteLine($"Ranked choice result: {Candidates[(int)rankedChoiceWinner].Attributes[0]} True condorcet {trueCondorcetWinnerExists} revealed condorcet {revealedCondorcetWinnerExists} rankedChoiceIsTrue {rankedChoiceWinnerIsTrue} rankedChoiceIsRevealed {rankedChoiceWinnerIsRevealed}");
            }

            return (trueCondorcetWinnerExists, revealedCondorcetWinnerExists, rankedChoiceWinnerIsTrue, rankedChoiceWinnerIsRevealed, revealedCondorcetWinnerIsTrue);
        }

        public void Setup()
        {
            SetupDimensions();
            SetupCandidates();
            SetupVoters();
        }

        public void SetupDimensions()
        {
            Dimensions = new Dimension[numDimensions];
            double weightLeftToAssign = 1.0;
            for (int d = 0; d < numDimensions; d++)
            {
                Dimensions[d] = new Dimension();
                Dimensions[d].VotersHaveVariablePreferences = Randomizer.NextDouble() < probabilityDimensionVariable;
                if (d == numDimensions - 1)
                    Dimensions[d].Weight = weightLeftToAssign;
                else
                {
                    Dimensions[d].Weight = eachDimensionEqualWeight ? 1.0 / (double) numDimensions : weightLeftToAssign * Randomizer.NextDouble() * maxProportionRemainingWeight;
                    weightLeftToAssign -= Dimensions[d].Weight;
                }
            }
        }

        public void SetupCandidates()
        {
            Candidates = new Candidate[numCandidates];
            for (int c = 0; c < numCandidates; c++)
            {
                Candidates[c] = new Candidate();
                SetupCandidate(c);
            }
        }

        public void SetupCandidate(int candidateIndex)
        {
            Candidate candidate = Candidates[candidateIndex];
            candidate.Attributes = new double[numDimensions];
            for (int d = 0; d < numDimensions; d++)
            {
                double weightPreviousCandidate = 0;
                double independentDraw = Randomizer.NextDouble();
                double previousCandidateDraw = 0;
                if (Randomizer.NextDouble() < probabilityBunching && candidateIndex > 1)
                {
                    weightPreviousCandidate = weightBunching;
                    int previousCandidate = Randomizer.Next(candidateIndex);
                    previousCandidateDraw = Candidates[previousCandidate].Attributes[d];
                }
                double weighted = weightPreviousCandidate * previousCandidateDraw + (1.0 - weightPreviousCandidate) * independentDraw;
                candidate.Attributes[d] = weighted;
            }
        }

        public string GetCandidateAttributes(int dimension)
        {
            return String.Join(",", Enumerable.Range(0, numCandidates).Select(x => Candidates[x].Attributes[dimension].ToString("#.##")));
        }

        public void SetupVoters()
        {
            Voters = new Voter[numVoters];
            for (int v = 0; v < numVoters; v++)
            {
                Voters[v] = new Voter();
                SetupVoter(Voters[v]);
            }
        }

        public void DoAssessments()
        {
            for (int v = 0; v < numVoters; v++)
            {
                AssessCandidates(Voters[v]);
            }
            if (bandwagonProportion > 0)
            {
                List<int> top = OrderedByFirstPlaceVotes(numCandidatesInBandwagon);
                foreach (Voter v in Voters)
                {
                    if (Randomizer.NextDouble() < bandwagonProportion)
                    {
                        ApplyBandwagonAdjustment(v, top);
                    }
                }
            }
        }

        public void SetupVoter(Voter voter)
        {
            voter.PreferredAttributes = new double[numDimensions];
            for (int d = 0; d < numDimensions; d++)
            {
                if (Dimensions[d].VotersHaveVariablePreferences == false)
                    voter.PreferredAttributes[d] = 1.0;
                else
                {
                    double r = Randomizer.NextDouble();
                    if (polarizationWeight != 0)
                    {
                        double r2 = polarizationWeight * (r > 0.5 ? 1 : 0) + (1.0 - polarizationWeight) * r;
                        //if (r > 0.4 && r < 0.6)
                        //    r2 = r;
                        r = r2;
                    }
                    voter.PreferredAttributes[d] = r;
                }
            }
        }

        public void AssessCandidates(Voter voter)
        {
            voter.CandidateScores = new double[numCandidates];
            for (int c = 0; c < numCandidates; c++)
            {
                double distance = 0;
                for (int d = 0; d < numDimensions; d++)
                {
                    double distanceThisDimension = Math.Abs(voter.PreferredAttributes[d] - Candidates[c].Attributes[d]);
                    distance += Math.Pow(distanceThisDimension, distancePower);
                }
                voter.CandidateScores[c] = distance;
            }
        }

        public void ApplyBandwagonAdjustment(Voter voter, List<int> bandwagonBonusForCandidates = null)
        {
            if (bandwagonBonusForCandidates != null)
            {
                double stdev = StdDev(voter.CandidateScores, false);
                double bonus = stdev * bandwagonEffect;
                foreach (int candidateReceivingBonus in bandwagonBonusForCandidates)
                    voter.CandidateScores[candidateReceivingBonus] -= bonus;
            }
        }

        public int GetLastChoice(Voter voter, bool[] eliminated)
        {
            int? choice = null;
            double worstScore = 0;
            for (int c = 0; c < numCandidates; c++)
            {
                if (eliminated[c] == false)
                {
                    if (choice == null || voter.CandidateScores[c] > voter.CandidateScores[(int)choice])
                    {
                        choice = c;
                        worstScore = voter.CandidateScores[c];
                    }
                }
            }
            return (int)choice;
        }

        public int GetFirstChoice(Voter voter, bool[] eliminated)
        {
            int? choice = null;
            double bestScore = 0;
            for (int c = 0; c < numCandidates; c++)
            {
                if (eliminated[c] == false)
                {
                    if (choice == null || voter.CandidateScores[c] < voter.CandidateScores[(int)choice])
                    {
                        choice = c;
                        bestScore = voter.CandidateScores[c];
                    }
                }
            }
            return (int)choice;
        }

        public bool VoterPrefersFirstCandidate(Voter voter, int candidate1, int candidate2)
        {
            return voter.CandidateScores[candidate1] < voter.CandidateScores[candidate2];
        }

        public bool VotersPreferFirstCandidate(int candidate1, int candidate2)
        {
            int firstCandidateVotes = 0;
            int secondCandidateVotes = 0;
            for (int v = 0; v < numVoters; v++)
            {
                bool firstCandidatePreferred = VoterPrefersFirstCandidate(Voters[v], candidate1, candidate2);
                if (firstCandidatePreferred)
                    firstCandidateVotes++;
                else
                    secondCandidateVotes++;
            }
            return firstCandidateVotes > secondCandidateVotes;
        }

        public bool CandidatePreferredToAllOthers(int candidate)
        {
            return Enumerable.Range(0, numCandidates).All(x => x == candidate || VotersPreferFirstCandidate(candidate, x));
        }

        public int? CondorcetWinner()
        {
            for (int c = 0; c < numCandidates; c++)
                if (CandidatePreferredToAllOthers(c))
                    return c;
            return null;
        }

        public int RankedChoiceWinner()
        {
            List<int> eliminationOrder = GetRankedChoiceEliminationOrder();
            return eliminationOrder.Last();
        }

        public List<int> OrderedByFirstPlaceVotes(int numToReturn)
        {
            List<int> eliminationOrder = GetRankedChoiceEliminationOrder(false);
            return eliminationOrder.Take(numToReturn).ToList();
        }

        public int MostFirstPlaceVotes()
        {
            List<int> eliminationOrder = GetRankedChoiceEliminationOrder(false);
            return eliminationOrder.First();
        }

        private List<int> GetRankedChoiceEliminationOrder(bool eliminateWorstFirst = true)
        {
            bool[] candidateEliminated = new bool[numCandidates];
            List<int> eliminationOrder = new List<int>();
            bool done = false;
            do
            {
                int[] lastChoiceVoterCount = new int[numCandidates];
                foreach (var voter in Voters)
                {
                    int lastChoiceIndexThisVoter = eliminateWorstFirst ? GetLastChoice(voter, candidateEliminated) : GetFirstChoice(voter, candidateEliminated);
                    lastChoiceVoterCount[lastChoiceIndexThisVoter]++;
                }
                int? lastChoiceIndex = null;
                int lastChoiceVotes = -1;
                for (int c = 0; c < numCandidates; c++)
                {
                    if (!candidateEliminated[c])
                    {
                        if (lastChoiceIndex == null || lastChoiceVoterCount[c] > lastChoiceVotes)
                        {
                            lastChoiceIndex = c;
                            lastChoiceVotes = lastChoiceVoterCount[c];
                        }
                    }
                }
                candidateEliminated[(int)lastChoiceIndex] = true;
                eliminationOrder.Add((int)lastChoiceIndex);
                done = eliminationOrder.Count() == numCandidates;
            }
            while (!done);
            return eliminationOrder;
        }

        public static double StdDev(IEnumerable<double> values,
            bool as_sample)
        {
            // Get the mean.
            double mean = values.Sum() / values.Count();

            // Get the sum of the squares of the differences
            // between the values and the mean.
            var squares_query =
                from double value in values
                select (value - mean) * (value - mean);
            double sum_of_squares = squares_query.Sum();

            if (as_sample)
            {
                return Math.Sqrt(sum_of_squares / (values.Count() - 1));
            }
            else
            {
                return Math.Sqrt(sum_of_squares / values.Count());
            }
        }

        static void Main(string[] args)
        {
            Program p = new Program();
            p.RepeatedExecution();
        }
    }
}
