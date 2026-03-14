using System;
using System.Collections.Generic;

public static class PyramidSolver
{
    private const int MAX_STATES = 2500000;
    private const int MAX_BUCKETS = 500;

    public class SolverResult
    {
        public bool IsSolved;
        public int StatesVisited;
        public int RecyclesUsed;
        public int MaxDepth;
        public int StartBranching;
        public int Backtracking;
        public int Inversions;
    }

    // Класс для кэширования памяти (Zero GC Allocations)
    public class SolverContext
    {
        public HashSet<ulong> Visited = new HashSet<ulong>();
        public Stack<SearchNode>[] Buckets = new Stack<SearchNode>[MAX_BUCKETS];
        public int[] Ranks = new int[52];
        public MoveCommand[] MoveBuffer = new MoveCommand[128];
        public int[] ExpBuffer = new int[28];

        public SolverContext()
        {
            for (int i = 0; i < MAX_BUCKETS; i++) Buckets[i] = new Stack<SearchNode>(5000);
        }

        public void ClearForNewSolve()
        {
            Visited.Clear();
            for (int i = 0; i < MAX_BUCKETS; i++) Buckets[i].Clear();
        }
    }

    public struct State : IEquatable<State>
    {
        public uint TableauMask;
        public uint StockMask;
        public byte Cursor;
        public byte Recycles;

        public ulong GetHash() => TableauMask | ((ulong)StockMask << 28) | ((ulong)Cursor << 52) | ((ulong)Recycles << 58);
        public bool Equals(State other) => TableauMask == other.TableauMask && StockMask == other.StockMask && Cursor == other.Cursor && Recycles == other.Recycles;
        public override int GetHashCode() => GetHash().GetHashCode();
    }

    public struct SearchNode { public State State; public int Depth; }

    public struct MoveCommand : IComparable<MoveCommand>
    {
        public State State;
        public int Priority;
        public int CompareTo(MoveCommand other) => this.Priority.CompareTo(other.Priority);
    }

    private static readonly uint[] BlockingMasks = new uint[28];
    private static readonly ulong[] AncestryMasks = new ulong[28];

    static PyramidSolver()
    {
        int cardIndex = 0;
        for (int row = 0; row < 7; row++)
        {
            for (int col = 0; col <= row; col++)
            {
                uint mask = 0;
                if (row < 6)
                {
                    mask = (1u << (cardIndex + row + 1)) | (1u << (cardIndex + row + 2));
                }
                BlockingMasks[cardIndex] = mask;
                cardIndex++;
            }
        }
        for (int i = 0; i < 28; i++) AncestryMasks[i] = GetAncestry(i, GetRow(i));
    }

    private static int GetRow(int index)
    {
        if (index == 0) return 0;
        if (index <= 2) return 1;
        if (index <= 5) return 2;
        if (index <= 9) return 3;
        if (index <= 14) return 4;
        if (index <= 20) return 5;
        return 6;
    }

    private static ulong GetAncestry(int index, int row)
    {
        if (row >= 6) return 0;
        int left = index + row + 1;
        int right = index + row + 2;
        ulong mask = (1ul << left) | (1ul << right);
        mask |= GetAncestry(left, row + 1);
        mask |= GetAncestry(right, row + 1);
        return mask;
    }

    private static int PopCount(uint i)
    {
        i = i - ((i >> 1) & 0x55555555);
        i = (i & 0x33333333) + ((i >> 2) & 0x33333333);
        return (int)((((i + (i >> 4)) & 0x0F0F0F0F) * 0x01010101) >> 24);
    }

    // Теперь метод полностью синхронный, так как выполняется в фоновом потоке
    public static void Solve(Deal initialDeal, int maxRecycles, SolverContext ctx, SolverResult resultOut)
    {
        ctx.ClearForNewSolve();

        int tIdx = 0;
        for (int r = 0; r < 7; r++)
            for (int c = 0; c <= r; c++) ctx.Ranks[tIdx++] = initialDeal.tableau[r][c].Card.rank;

        var stockArr = initialDeal.stock.ToArray();
        for (int i = 0; i < stockArr.Length; i++) ctx.Ranks[28 + i] = stockArr[i].Card.rank;

        CalculateStaticMetrics(ctx.Ranks, resultOut);

        State startState = new State { TableauMask = (1u << 28) - 1, StockMask = (1u << 24) - 1, Cursor = 0, Recycles = 0 };

        int startBucket = PopCount(startState.TableauMask) * 2;
        ctx.Buckets[startBucket].Push(new SearchNode { State = startState, Depth = 0 });
        int currentBucket = startBucket;

        int statesVisited = 0;
        int maxDepth = 0;
        bool solved = false;
        State winningState = startState;

        while (true)
        {
            while (currentBucket < MAX_BUCKETS && ctx.Buckets[currentBucket].Count == 0) currentBucket++;
            if (currentBucket >= MAX_BUCKETS) break;

            var node = ctx.Buckets[currentBucket].Pop();
            statesVisited++;

            if (node.Depth > maxDepth) maxDepth = node.Depth;

            if (node.State.TableauMask == 0)
            {
                solved = true;
                winningState = node.State;
                break;
            }

            if (statesVisited > MAX_STATES) break;

            ulong hash = node.State.GetHash();
            if (!ctx.Visited.Add(hash)) continue;

            int movesCount = GenerateMoves(node.State, ctx.Ranks, maxRecycles, ctx.MoveBuffer, ctx.ExpBuffer);
            if (node.Depth == 0) resultOut.StartBranching = movesCount;

            Array.Sort(ctx.MoveBuffer, 0, movesCount);

            for (int i = 0; i < movesCount; i++)
            {
                State nextState = ctx.MoveBuffer[i].State;
                int nextDepth = node.Depth + 1;
                int b = nextDepth + (PopCount(nextState.TableauMask) * 2) + (nextState.Recycles * 2);
                if (b >= MAX_BUCKETS) b = MAX_BUCKETS - 1;

                ctx.Buckets[b].Push(new SearchNode { State = nextState, Depth = nextDepth });
                if (b < currentBucket) currentBucket = b;
            }
        }

        resultOut.IsSolved = solved;
        resultOut.StatesVisited = statesVisited;
        resultOut.MaxDepth = maxDepth;
        resultOut.RecyclesUsed = solved ? winningState.Recycles : 0;
        resultOut.Backtracking = statesVisited - maxDepth;
    }

    private static void CalculateStaticMetrics(int[] ranks, SolverResult res)
    {
        res.Inversions = 0;
        for (int parent = 0; parent < 28; parent++)
        {
            int r1 = ranks[parent];
            if (r1 == 13) continue;

            int needed = 13 - r1;
            ulong childrenMask = AncestryMasks[parent];

            for (int child = 0; child < 28; child++)
            {
                if (((childrenMask >> child) & 1) != 0 && ranks[child] == needed)
                {
                    res.Inversions++;
                }
            }
        }
    }

    private static int GenerateMoves(State st, int[] ranks, int maxRecycles, MoveCommand[] outMoves, int[] expBuffer)
    {
        int moveCount = 0;
        int expCount = 0;
        for (int i = 0; i < 28; i++)
        {
            if ((st.TableauMask & (1u << i)) != 0 && (st.TableauMask & BlockingMasks[i]) == 0) expBuffer[expCount++] = i;
        }

        int w = -1;
        for (int i = st.Cursor - 1; i >= 0; i--) if ((st.StockMask & (1u << i)) != 0) { w = i; break; }

        int s = -1;
        for (int i = st.Cursor; i < 24; i++) if ((st.StockMask & (1u << i)) != 0) { s = i; break; }

        for (int i = 0; i < expCount; i++)
        {
            if (ranks[expBuffer[i]] == 13)
            {
                State next = st; next.TableauMask &= ~(1u << expBuffer[i]);
                outMoves[0] = new MoveCommand { State = next, Priority = 100 };
                return 1;
            }
        }
        if (w != -1 && ranks[28 + w] == 13) { outMoves[0] = new MoveCommand { State = st, Priority = 95 }; outMoves[0].State.StockMask &= ~(1u << w); return 1; }
        if (s != -1 && ranks[28 + s] == 13) { outMoves[0] = new MoveCommand { State = st, Priority = 90 }; outMoves[0].State.StockMask &= ~(1u << s); return 1; }

        for (int i = 0; i < expCount; i++)
            for (int j = i + 1; j < expCount; j++)
                if (ranks[expBuffer[i]] + ranks[expBuffer[j]] == 13)
                {
                    State next = st; next.TableauMask &= ~(1u << expBuffer[i]); next.TableauMask &= ~(1u << expBuffer[j]);
                    outMoves[moveCount++] = new MoveCommand { State = next, Priority = 85 };
                }

        if (w != -1)
            for (int i = 0; i < expCount; i++)
                if (ranks[expBuffer[i]] + ranks[28 + w] == 13)
                {
                    State next = st; next.TableauMask &= ~(1u << expBuffer[i]); next.StockMask &= ~(1u << w);
                    outMoves[moveCount++] = new MoveCommand { State = next, Priority = 70 };
                }

        if (s != -1)
            for (int i = 0; i < expCount; i++)
                if (ranks[expBuffer[i]] + ranks[28 + s] == 13)
                {
                    State next = st; next.TableauMask &= ~(1u << expBuffer[i]); next.StockMask &= ~(1u << s);
                    outMoves[moveCount++] = new MoveCommand { State = next, Priority = 60 };
                }

        if (w != -1 && s != -1 && ranks[28 + w] + ranks[28 + s] == 13)
        {
            State next = st; next.StockMask &= ~(1u << w); next.StockMask &= ~(1u << s);
            outMoves[moveCount++] = new MoveCommand { State = next, Priority = 50 };
        }

        if (s != -1)
        {
            State next = st; next.Cursor = (byte)(s + 1);
            outMoves[moveCount++] = new MoveCommand { State = next, Priority = 10 };
        }
        else if (st.Recycles < maxRecycles && st.Cursor > 0)
        {
            State next = st; next.Cursor = 0; next.Recycles++;
            outMoves[moveCount++] = new MoveCommand { State = next, Priority = 5 };
        }

        return moveCount;
    }
}