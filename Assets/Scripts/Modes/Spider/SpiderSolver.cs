using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using System.Diagnostics;

public static class SpiderSolver
{
    private const int MAX_DEPTH = 2500;
    private const int MAX_STATES = 1500000;

    // --- œ”¡À»◊Õ€≈ —“–” “”–€ ---
    [Serializable]
    public class ExtendedSolverResult
    {
        public bool IsSolved;
        public int Score;
        public int Moves;
        public int StatesVisited;
        public List<MoveCommand> MoveSequence = new List<MoveCommand>();
    }

    public class SpiderMetrics
    {
        public int SuitsCount;
        public int InitialLegalMoves;
        public int KingsOnHidden;
        public int HighBlockersPenalty;
        public int BuriedAcesAndTwos;
        public int HiddenRankBreaks;
        public int HiddenInSuitPairs;
        public int DeepestKing;
        public int DistanceToFirstEmpty;
    }

    public enum MoveType { MoveColumn, StockDraw }

    public struct MoveCommand
    {
        public MoveType Type;
        public int From, To, Count;
        public bool IsInSuit, IsSplit, RevealsHidden, EmptiesColumn;
    }

    // --- ¬Õ”“–≈ÕÕ»≈ —“–” “”–€ ---
    public struct Card { public byte rank; public byte suit; public bool faceUp; }

    public struct InternalDeal
    {
        public List<Card>[] tableau;
        public List<Card> stock;

        public InternalDeal Clone()
        {
            InternalDeal d = new InternalDeal();
            d.tableau = new List<Card>[10];
            for (int i = 0; i < 10; i++) d.tableau[i] = new List<Card>(this.tableau[i]);
            d.stock = new List<Card>(this.stock);
            return d;
        }
    }

    private class SearchNode
    {
        public InternalDeal State;
        public SearchNode Parent;
        public MoveCommand MoveMade;
        public int Depth;
        public int SuitsCompleted;

        public SearchNode(InternalDeal state, SearchNode parent, MoveCommand move, int depth, int suits)
        {
            State = state; Parent = parent; MoveMade = move; Depth = depth; SuitsCompleted = suits;
        }
    }

    private class PriorityQueue<T>
    {
        private List<KeyValuePair<T, int>> elements = new List<KeyValuePair<T, int>>();
        public int Count => elements.Count;

        public void Enqueue(T item, int priority)
        {
            elements.Add(new KeyValuePair<T, int>(item, priority));
            int ci = elements.Count - 1;
            while (ci > 0)
            {
                int pi = (ci - 1) / 2;
                if (elements[ci].Value >= elements[pi].Value) break;
                var tmp = elements[ci]; elements[ci] = elements[pi]; elements[pi] = tmp; ci = pi;
            }
        }

        public T Dequeue()
        {
            int li = elements.Count - 1;
            var frontItem = elements[0].Key;
            elements[0] = elements[li];
            elements.RemoveAt(li);
            --li;
            int pi = 0;
            while (true)
            {
                int ci = pi * 2 + 1;
                if (ci > li) break;
                int rc = ci + 1;
                if (rc <= li && elements[rc].Value < elements[ci].Value) ci = rc;
                if (elements[pi].Value <= elements[ci].Value) break;
                var tmp = elements[pi]; elements[pi] = elements[ci]; elements[ci] = tmp; pi = ci;
            }
            return frontItem;
        }
    }

    // =================================================================================
    // ¿—»Õ’–ŒÕÕ€… Ã≈“Œƒ œŒ»— ¿ (A*)
    // =================================================================================
    public static IEnumerator SolveAsync(Deal initialDeal, int suitsCount, float frameBudgetMs, ExtendedSolverResult resultOut)
    {
        InternalDeal internalRoot = ConvertToInternal(initialDeal);
        var openSet = new PriorityQueue<SearchNode>();
        var closedSet = new HashSet<ulong>();

        var root = new SearchNode(internalRoot, null, default, 0, 0);
        openSet.Enqueue(root, 0);

        int statesVisited = 0;
        SearchNode winningNode = null;

        Stopwatch sw = Stopwatch.StartNew();

        while (openSet.Count > 0)
        {
            if (sw.ElapsedMilliseconds >= frameBudgetMs)
            {
                yield return null;
                sw.Restart();
            }

            if (statesVisited >= MAX_STATES) break;

            var current = openSet.Dequeue();
            statesVisited++;

            int cardsLeft = current.State.tableau.Sum(c => c.Count) + current.State.stock.Count;

            if (cardsLeft == 0 || current.SuitsCompleted == 8)
            {
                winningNode = current;
                break;
            }

            if (current.Depth > MAX_DEPTH) continue;

            ulong stateHash = ComputeSymmetricHash(current.State);
            if (closedSet.Contains(stateHash)) continue;
            closedSet.Add(stateHash);

            List<MoveCommand> moves = (suitsCount == 1) ? GetMoves_1Suit(current.State) : GetMoves_MultiSuit(current.State, suitsCount);

            foreach (var move in moves)
            {
                int newSuits = 0;
                InternalDeal nextState = ApplyMove(current.State, move, out newSuits);
                int totalSuits = current.SuitsCompleted + newSuits;

                int fCost = (suitsCount == 1) ? Heuristic_1Suit(nextState, totalSuits) + current.Depth : Heuristic_MultiSuit(nextState, totalSuits, suitsCount) + current.Depth;

                SearchNode nextNode = new SearchNode(nextState, current, move, current.Depth + 1, totalSuits);
                openSet.Enqueue(nextNode, fCost);
            }
        }

        resultOut.StatesVisited = statesVisited;
        resultOut.IsSolved = winningNode != null;

        if (winningNode != null)
        {
            List<MoveCommand> path = new List<MoveCommand>();
            SearchNode curr = winningNode;
            while (curr.Parent != null) { path.Add(curr.MoveMade); curr = curr.Parent; }
            path.Reverse();

            resultOut.Moves = path.Count;
            resultOut.MoveSequence = path;
        }
    }

    // =================================================================================
    // Œ÷≈Õ ¿ —ÀŒ∆ÕŒ—“» (SDS)
    // =================================================================================
    public static SpiderMetrics CalculateStaticMetrics(Deal d, int suitsCount)
    {
        SpiderMetrics m = new SpiderMetrics { SuitsCount = suitsCount };
        int minDepth = 999;

        var exposedCards = d.tableau.Where(c => c.Count > 0).Select(c => c.Last().Card).ToList();
        foreach (var c1 in exposedCards)
            foreach (var c2 in exposedCards)
                if (c1.suit != c2.suit || c1.rank != c2.rank)
                    if (c2.rank == c1.rank + 1) m.InitialLegalMoves++;

        foreach (var col in d.tableau)
        {
            if (col.Count < minDepth) minDepth = col.Count;
            if (col.Count == 0) continue;

            int hiddenCount = col.Count(c => !c.FaceUp);
            var firstRevealed = col.FirstOrDefault(c => c.FaceUp);

            if (firstRevealed != null)
            {
                if (firstRevealed.Card.rank == 13) m.KingsOnHidden++;
                if (firstRevealed.Card.rank >= 11) m.HighBlockersPenalty += (firstRevealed.Card.rank - 10) * hiddenCount * 50;
            }

            for (int i = 0; i < hiddenCount; i++)
                if (col[i].Card.rank <= 2) m.BuriedAcesAndTwos += (hiddenCount - i);

            for (int i = 0; i < hiddenCount - 1; i++)
            {
                var top = col[i].Card;
                var bottom = col[i + 1].Card;
                if (bottom.rank == top.rank - 1 && bottom.suit == top.suit) m.HiddenInSuitPairs++;
                else m.HiddenRankBreaks++;

                if (top.rank == 13) m.DeepestKing = Mathf.Max(m.DeepestKing, hiddenCount - i);
            }
            if (hiddenCount > 0 && col[hiddenCount - 1].Card.rank == 13) m.DeepestKing = Mathf.Max(m.DeepestKing, 1);
        }
        m.DistanceToFirstEmpty = minDepth;
        return m;
    }

    public static int CalculateDifficultyScore(SpiderMetrics m)
    {
        int score = 0;
        if (m.SuitsCount == 1) score += 15;
        else if (m.SuitsCount == 2) score += 45;
        else if (m.SuitsCount == 4) score += 80;

        score += (m.KingsOnHidden * 4);
        score += (m.DeepestKing * 2);
        score += (m.BuriedAcesAndTwos * 1);
        score += (m.HighBlockersPenalty / 100);
        score += (m.HiddenRankBreaks / 2);
        score -= (m.InitialLegalMoves * 2);

        return Mathf.Max(1, score);
    }

    public static Difficulty ClassifyDifficulty(int sdsScore, int suitsCount)
    {
        if (suitsCount == 1)
        {
            return sdsScore <= 35 ? Difficulty.Easy : Difficulty.Medium;
        }
        else if (suitsCount == 2)
        {
            if (sdsScore <= 60) return Difficulty.Easy;
            if (sdsScore <= 75) return Difficulty.Medium;
            return Difficulty.Hard;
        }
        else // 4 Ï‡ÒÚË
        {
            if (sdsScore <= 95) return Difficulty.Medium;
            return Difficulty.Hard; // ÕÂÚ ‚ÂıÌÂÈ „‡ÌËˆ˚ (Grandmaster Û‰‡ÎÂÌ)
        }
    }

    // =================================================================================
    // ¬Õ”“–≈ÕÕﬂﬂ ÀŒ√» ¿ ’ŒƒŒ¬ » ›¬–»—“» 
    // =================================================================================
    private static List<MoveCommand> GetMoves_1Suit(InternalDeal d)
    {
        List<MoveCommand> moves = new List<MoveCommand>();
        bool hasEmptyCols = d.tableau.Any(c => c.Count == 0);

        for (int from = 0; from < 10; from++)
        {
            if (d.tableau[from].Count == 0) continue;
            int maxHeight = GetValidSequenceHeight(d.tableau[from]);
            int hiddenInFrom = d.tableau[from].Count(c => !c.faceUp);
            int faceUpCount = d.tableau[from].Count - hiddenInFrom;

            for (int count = maxHeight; count >= 1; count--)
            {
                var movingCard = d.tableau[from][d.tableau[from].Count - count];
                bool isSplit = (count < maxHeight);
                if (isSplit) continue;

                bool revealsHidden = (count == faceUpCount) && (hiddenInFrom > 0);
                bool emptiesCol = (count == faceUpCount) && (hiddenInFrom == 0);
                bool movedToEmpty = false;

                for (int to = 0; to < 10; to++)
                {
                    if (from == to) continue;
                    if (d.tableau[to].Count == 0)
                    {
                        if (emptiesCol || movedToEmpty) continue;
                        movedToEmpty = true;
                        moves.Add(new MoveCommand { Type = MoveType.MoveColumn, From = from, To = to, Count = count, IsInSuit = true, IsSplit = false, RevealsHidden = revealsHidden, EmptiesColumn = emptiesCol });
                    }
                    else if (d.tableau[to].Last().rank == movingCard.rank + 1)
                    {
                        moves.Add(new MoveCommand { Type = MoveType.MoveColumn, From = from, To = to, Count = count, IsInSuit = true, IsSplit = false, RevealsHidden = revealsHidden, EmptiesColumn = emptiesCol });
                    }
                }
            }
        }

        if (d.stock.Count > 0 && !hasEmptyCols) moves.Add(new MoveCommand { Type = MoveType.StockDraw });

        if (hasEmptyCols && d.stock.Count > 0)
        {
            var fills = moves.Where(m => m.Type == MoveType.MoveColumn && d.tableau[m.To].Count == 0).ToList();
            if (fills.Count > 0) return fills;
        }

        var perfectReveals = moves.Where(m => m.Type == MoveType.MoveColumn && m.RevealsHidden && m.IsInSuit).ToList();
        if (perfectReveals.Count > 0) return new List<MoveCommand> { perfectReveals[0] };

        var anyReveals = moves.Where(m => m.Type == MoveType.MoveColumn && m.RevealsHidden).ToList();
        if (anyReveals.Count > 0) return anyReveals;

        var perfectMerges = moves.Where(m => m.Type == MoveType.MoveColumn && m.IsInSuit && d.tableau[m.To].Count > 0 && !m.EmptiesColumn).ToList();
        if (perfectMerges.Count > 0) return perfectMerges;

        return moves;
    }

    private static int Heuristic_1Suit(InternalDeal d, int suitsCompleted)
    {
        int h = 0;
        h -= suitsCompleted * 100000;
        h += d.tableau.Sum(c => c.Count(x => !x.faceUp)) * 10000;
        h -= d.tableau.Count(c => c.Count == 0) * 5000;

        foreach (var col in d.tableau)
        {
            int hidden = col.Count(x => !x.faceUp);
            int seqLength = 1;
            for (int i = hidden; i < col.Count - 1; i++)
            {
                if (col[i + 1].rank == col[i].rank - 1) { seqLength++; h -= seqLength * 200; }
                else { seqLength = 1; h += 3000; }
            }
        }
        return h;
    }

    private static List<MoveCommand> GetMoves_MultiSuit(InternalDeal d, int suitsCount)
    {
        List<MoveCommand> allMoves = new List<MoveCommand>();
        bool hasEmptyCols = d.tableau.Any(c => c.Count == 0);
        int totalHidden = d.tableau.Sum(c => c.Count(x => !x.faceUp));
        bool isEndgame = (totalHidden == 0 && d.stock.Count == 0);

        for (int from = 0; from < 10; from++)
        {
            if (d.tableau[from].Count == 0) continue;
            int maxHeight = GetValidSequenceHeight(d.tableau[from]);

            int hiddenInFrom = d.tableau[from].Count(c => !c.faceUp);
            int faceUpCount = d.tableau[from].Count - hiddenInFrom;

            for (int count = maxHeight; count >= 1; count--)
            {
                var movingCard = d.tableau[from][d.tableau[from].Count - count];
                bool isSplit = (count < maxHeight);
                bool isPerfectSplit = false;

                if (isSplit)
                {
                    var parent = d.tableau[from][d.tableau[from].Count - count - 1];
                    isPerfectSplit = (parent.suit == movingCard.suit && parent.rank == movingCard.rank + 1);
                }

                bool revealsHidden = (count == faceUpCount) && (hiddenInFrom > 0);
                bool emptiesCol = (count == faceUpCount) && (hiddenInFrom == 0);
                bool movedToEmpty = false;

                for (int to = 0; to < 10; to++)
                {
                    if (from == to) continue;

                    if (d.tableau[to].Count == 0)
                    {
                        if (emptiesCol || movedToEmpty || isPerfectSplit) continue;
                        movedToEmpty = true;
                        allMoves.Add(new MoveCommand { Type = MoveType.MoveColumn, From = from, To = to, Count = count, IsInSuit = false, IsSplit = isSplit, RevealsHidden = revealsHidden, EmptiesColumn = emptiesCol });
                    }
                    else
                    {
                        if (isPerfectSplit && d.tableau[to].Last().suit == movingCard.suit) continue;

                        var target = d.tableau[to].Last();
                        if (target.rank == movingCard.rank + 1)
                        {
                            bool sameSuit = (target.suit == movingCard.suit);
                            bool isPurposeful = true;

                            if (!sameSuit && !revealsHidden && !emptiesCol)
                            {
                                isPurposeful = false;
                                if (d.tableau[from].Count > count + hiddenInFrom)
                                {
                                    var exposed = d.tableau[from][d.tableau[from].Count - count - 1];
                                    for (int i = 0; i < 10; i++)
                                    {
                                        if (i == from || i == to) continue;
                                        if (d.tableau[i].Count > 0 && d.tableau[i].Last().suit == exposed.suit && d.tableau[i].Last().rank == exposed.rank + 1)
                                        { isPurposeful = true; break; }
                                    }
                                    if (!isPurposeful)
                                    {
                                        for (int i = 0; i < 10; i++)
                                        {
                                            if (i == from || i == to || d.tableau[i].Count == 0) continue;
                                            int h = GetValidSequenceHeight(d.tableau[i]);
                                            var child = d.tableau[i][d.tableau[i].Count - h];
                                            if (child.suit == exposed.suit && child.rank == exposed.rank - 1)
                                            { isPurposeful = true; break; }
                                        }
                                    }
                                    if (!isPurposeful && isEndgame)
                                    {
                                        for (int i = 0; i < 10; i++)
                                        {
                                            if (i == from || i == to || d.tableau[i].Count == 0) continue;
                                            if (d.tableau[i].Last().rank == exposed.rank + 1) { isPurposeful = true; break; }
                                        }
                                    }
                                }
                                if (!isPurposeful && hasEmptyCols && !isSplit) isPurposeful = true;
                            }

                            if (isPurposeful)
                                allMoves.Add(new MoveCommand { Type = MoveType.MoveColumn, From = from, To = to, Count = count, IsInSuit = sameSuit, IsSplit = isSplit, RevealsHidden = revealsHidden, EmptiesColumn = emptiesCol });
                        }
                    }
                }
            }
        }

        var perfectReveals = allMoves.Where(m => m.RevealsHidden && m.IsInSuit && !m.IsSplit).ToList();
        if (perfectReveals.Count > 0) return perfectReveals;

        var anyReveals = allMoves.Where(m => m.RevealsHidden).ToList();
        var perfectMerges = allMoves.Where(m => m.IsInSuit && !m.IsSplit && d.tableau[m.To].Count > 0 && !m.EmptiesColumn).ToList();
        var emptyColMoves = allMoves.Where(m => m.EmptiesColumn).ToList();
        var inSuitBuilds = allMoves.Where(m => m.IsInSuit).ToList();

        var highTier = new List<MoveCommand>();
        highTier.AddRange(anyReveals);
        foreach (var m in perfectMerges) if (!highTier.Contains(m)) highTier.Add(m);
        if (!hasEmptyCols) foreach (var m in emptyColMoves) if (!highTier.Contains(m)) highTier.Add(m);

        if (highTier.Count > 0) return highTier;
        if (inSuitBuilds.Count > 0) return inSuitBuilds;

        if (d.stock.Count > 0 && !hasEmptyCols) allMoves.Add(new MoveCommand { Type = MoveType.StockDraw });

        return allMoves;
    }

    private static int Heuristic_MultiSuit(InternalDeal d, int suitsCompleted, int suitsCount)
    {
        int h = 0;
        h -= suitsCompleted * 2000000;

        int totalHidden = d.tableau.Sum(c => c.Count(x => !x.faceUp));
        h += totalHidden * 10000;

        int emptyCols = d.tableau.Count(c => c.Count == 0);
        h -= emptyCols * 15000;

        int stockDealsDone = 5 - (d.stock.Count / 10);
        if (totalHidden > 0) h += (stockDealsDone * stockDealsDone) * (totalHidden * 100);

        var topCards = new List<Card>();
        foreach (var c in d.tableau) if (c.Count > 0 && c.Last().faceUp) topCards.Add(c.Last());

        foreach (var col in d.tableau)
        {
            int hidden = col.Count(x => !x.faceUp);
            for (int i = 0; i < hidden; i++)
            {
                var hiddenCard = col[i];
                int depth = hidden - i;
                if (hiddenCard.rank == 13 && emptyCols > 0) h -= 1000 / depth;
                foreach (var top in topCards)
                    if (top.rank == hiddenCard.rank + 1 && top.suit == hiddenCard.suit) h -= 800 / depth;
            }

            if (col.Count == hidden) continue;

            int inSuitStreak = 1;
            for (int i = hidden; i < col.Count - 1; i++)
            {
                var top = col[i]; var bot = col[i + 1];
                if (bot.rank == top.rank - 1)
                {
                    if (bot.suit == top.suit) inSuitStreak++;
                    else
                    {
                        h -= (inSuitStreak * inSuitStreak) * (suitsCount == 4 ? 400 : 300);
                        inSuitStreak = 1;
                        h += (hidden > 0) ? (hidden * 250) : (suitsCount == 2 ? 200 : 50);
                    }
                }
                else
                {
                    h -= (inSuitStreak * inSuitStreak) * (suitsCount == 4 ? 400 : 300);
                    inSuitStreak = 1;
                    h += 5000;
                }
            }
            h -= (inSuitStreak * inSuitStreak) * (suitsCount == 4 ? 400 : 300);
        }
        return h;
    }

    private static ulong ComputeSymmetricHash(InternalDeal d)
    {
        ulong[] colHashes = new ulong[10];
        for (int i = 0; i < 10; i++)
        {
            var col = d.tableau[i];
            ulong h = 17;
            int hidden = col.Count(c => !c.faceUp);
            h = h * 1009 + (ulong)hidden;

            for (int k = 0; k < hidden; k++)
            {
                var card = col[k];
                h = h * 31 + (ulong)(k * 200 + card.rank + card.suit * 14);
            }
            for (int k = hidden; k < col.Count; k++)
            {
                var card = col[k];
                h = h * 1009 + (ulong)card.rank + ((ulong)card.suit * 100);
            }
            colHashes[i] = h;
        }

        Array.Sort(colHashes);
        ulong finalHash = 17;
        foreach (var ch in colHashes) finalHash = unchecked(finalHash * 1009 + ch);
        finalHash = unchecked(finalHash ^ ((ulong)d.stock.Count * 987654321ul));
        return finalHash;
    }

    private static InternalDeal ApplyMove(InternalDeal old, MoveCommand m, out int suitsCompleted)
    {
        suitsCompleted = 0;
        InternalDeal d = old.Clone();

        if (m.Type == MoveType.MoveColumn)
        {
            var src = d.tableau[m.From];
            var dst = d.tableau[m.To];
            var range = src.GetRange(src.Count - m.Count, m.Count);
            src.RemoveRange(src.Count - m.Count, m.Count);

            if (src.Count > 0) { var top = src[src.Count - 1]; top.faceUp = true; src[src.Count - 1] = top; }
            dst.AddRange(range);
            if (CheckAndRemoveFullSuit(dst)) suitsCompleted++;
        }
        else
        {
            for (int i = 0; i < 10; i++)
            {
                if (d.stock.Count > 0)
                {
                    var c = d.stock[d.stock.Count - 1];
                    d.stock.RemoveAt(d.stock.Count - 1);
                    c.faceUp = true;
                    d.tableau[i].Add(c);
                    if (CheckAndRemoveFullSuit(d.tableau[i])) suitsCompleted++;
                }
            }
        }
        return d;
    }

    private static bool CheckAndRemoveFullSuit(List<Card> pile)
    {
        if (pile.Count < 13) return false;
        var top = pile.Last();
        if (top.rank != 1) return false;

        int startIndex = pile.Count - 13;
        var baseSuit = pile[startIndex].suit;

        for (int i = 0; i < 13; i++)
            if (!pile[startIndex + i].faceUp || pile[startIndex + i].suit != baseSuit || pile[startIndex + i].rank != (13 - i))
                return false;

        pile.RemoveRange(startIndex, 13);
        if (pile.Count > 0) { var newTop = pile[pile.Count - 1]; newTop.faceUp = true; pile[pile.Count - 1] = newTop; }
        return true;
    }

    private static int GetValidSequenceHeight(List<Card> pile)
    {
        if (pile.Count == 0) return 0;
        int h = 1;
        for (int i = pile.Count - 1; i > 0; i--)
        {
            var curr = pile[i]; var prev = pile[i - 1];
            if (!prev.faceUp) break;
            if (curr.suit == prev.suit && curr.rank == prev.rank - 1) h++;
            else break;
        }
        return h;
    }

    private static InternalDeal ConvertToInternal(Deal d)
    {
        InternalDeal id = new InternalDeal();
        id.tableau = new List<Card>[10];
        for (int i = 0; i < 10; i++)
        {
            id.tableau[i] = new List<Card>();
            foreach (var c in d.tableau[i]) id.tableau[i].Add(new Card { rank = (byte)c.Card.rank, suit = (byte)(int)c.Card.suit, faceUp = c.FaceUp });
        }
        id.stock = new List<Card>();
        var stockArr = d.stock.ToArray();
        Array.Reverse(stockArr);
        foreach (var c in stockArr) id.stock.Add(new Card { rank = (byte)c.Card.rank, suit = (byte)(int)c.Card.suit, faceUp = c.FaceUp });
        return id;
    }
}