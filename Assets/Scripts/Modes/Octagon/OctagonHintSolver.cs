using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

public struct OctagonHintMove
{
    public enum MoveType { Foundation, WasteToFoundation, TableauToTableau, WasteToTableau, StockDraw, Recycle }
    public MoveType Type;
    public int From;
    public int To;
    public int TargetFoundation;
}

public class OctagonHintSolver : MonoBehaviour
{
    // Ограничители, чтобы поиск не работал бесконечно и не фризил игру
    private const float FRAME_BUDGET_MS = 14f;

    [Header("Performance")]
    // Раньше было жёсткой константой = 500. Полное решение партии с нуля (76 карт в
    // колоде + 20 на столе, все должны в итоге пройти через сброс/стол на дом) легко
    // требует 600-900+ ходов. Лог с первого хода партии показал: avgBranch~1.1 (почти
    // нет развилок), а best=76/104 не сдвигался с места 1.5 секунды подряд при том что
    // visited утроился - классический признак того, что поиск упирался в глубину 500
    // и просто не мог раскрыть узлы дальше, а не в нехватку узлов/времени. При таком
    // низком ветвлении идти глубже почти ничего не стоит по числу узлов, поэтому лимит
    // можно смело поднять с большим запасом.
    [Tooltip("Максимальная глубина (число ходов) одного пути поиска")]
    [SerializeField] private int maxDepth = 2000;
    [Tooltip("Максимум узлов, которые солвер посетит за один запрос подсказки")]
    [SerializeField] private int maxSearchNodes = 250000;
    [Tooltip("Сколько лучших дочерних ходов оставлять на узел. Меньше = быстрее, но выше риск не найти путь")]
    [SerializeField] private int maxChildrenPerNode = 24;
    // Лог с реального старта партии показал ~55-60 тыс. узлов/сек и что с нуля партии
    // нужно ощутимо больше 100k узлов, чтобы дойти до победы (поздний перезапуск с
    // foundSum=12 нашёл путь за 63186 узлов/1.1s - а полное решение с нуля явно тяжелее).
    // Подняты оба лимита; ниже добавлен отдельный "частичный путь" как подстраховка на
    // случай, если и этого бюджета не хватит.
    [Tooltip("Жёсткий лимит по реальному времени на один поиск (сек). Страхует UI от бесконечного 'Ищу победный ход...'")]
    [SerializeField] private float maxTotalSearchSeconds = 6f;

    [Header("Debug / Logging")]
    [Tooltip("Писать в консоль ход поиска: старт, периодический прогресс, итог")]
    [SerializeField] private bool logSearchProgress = true;
    [Tooltip("Как часто (в реальных секундах) писать heartbeat-лог во время поиска")]
    [SerializeField] private float progressLogIntervalSeconds = 0.5f;
    [Tooltip("Для скольких первых раскрытых узлов подробно логировать список ходов-кандидатов (0 = выключено)")]
    [SerializeField] private int detailedLogFirstNNodes = 5;

    private struct CardState { public byte suit; public byte rank; }

    private struct CandidateMove
    {
        public OctagonHintMove Move;
        public int FCost;
    }

    private class FastBoard
    {
        public CardState[,] tableau = new CardState[20, 13];
        public int[] tabLens = new int[20];
        public CardState[] stock = new CardState[104];
        public int stockCount;
        public CardState[] waste = new CardState[104];
        public int wasteCount;
        public byte[] foundations = new byte[8];
        // Реальная масть, лежащая на каждом из 8 домов (заполняется один раз при чтении
        // доски и не меняется - см. комментарий в GetFoundationIndex, почему это важно).
        public byte[] foundationSuit = new byte[8];
        public byte recyclesUsed;
        public int refillsTriggered;

        public ulong GetHash()
        {
            ulong hash = 17;
            ulong fHash = 0;
            for (int i = 0; i < 8; i++) fHash = (fHash << 4) | foundations[i];
            hash = hash * 397 + fHash;

            ulong[] groupHashes = new ulong[4];
            for (int g = 0; g < 4; g++)
            {
                ulong[] slotHashes = new ulong[5];
                for (int s = 0; s < 5; s++)
                {
                    ulong h = 17;
                    int idx = g * 5 + s;
                    for (int j = 0; j < tabLens[idx]; j++)
                        h = h * 31 + (ulong)(tableau[idx, j].suit * 13 + tableau[idx, j].rank);
                    slotHashes[s] = h;
                }
                Array.Sort(slotHashes);
                ulong gh = 19;
                foreach (var sh in slotHashes) gh = gh * 1009 + sh;
                groupHashes[g] = gh;
            }
            Array.Sort(groupHashes);
            foreach (var gh in groupHashes) hash = hash * 1009 + gh;

            hash ^= (ulong)stockCount * 1234567;

            ulong wHash = 17;
            for (int i = 0; i < wasteCount; i++) wHash = wHash * 31 + (ulong)(waste[i].suit * 13 + waste[i].rank);
            hash ^= wHash;

            hash ^= (ulong)recyclesUsed * 1010101;
            hash ^= (ulong)refillsTriggered * 8888888;
            return hash;
        }

        public FastBoard Clone()
        {
            FastBoard c = new FastBoard();
            Array.Copy(foundations, c.foundations, 8);
            Array.Copy(foundationSuit, c.foundationSuit, 8);
            Array.Copy(tabLens, c.tabLens, 20);
            for (int i = 0; i < 20; i++)
                for (int j = 0; j < tabLens[i]; j++)
                    c.tableau[i, j] = tableau[i, j];

            Array.Copy(stock, c.stock, stockCount);
            c.stockCount = stockCount;

            Array.Copy(waste, c.waste, wasteCount);
            c.wasteCount = wasteCount;

            c.recyclesUsed = recyclesUsed;
            c.refillsTriggered = refillsTriggered;
            return c;
        }

        // Копирует состояние другой доски в УЖЕ выделенные массивы этого объекта, без аллокаций.
        // Используется как переиспользуемый "черновик" для дешёвой оценки хода-кандидата
        // (хэш + эвристика), прежде чем решать, стоит ли делать полноценный Clone().
        public void CopyFrom(FastBoard other)
        {
            Array.Copy(other.foundations, foundations, 8);
            Array.Copy(other.foundationSuit, foundationSuit, 8);
            Array.Copy(other.tabLens, tabLens, 20);
            for (int i = 0; i < 20; i++)
                for (int j = 0; j < other.tabLens[i]; j++)
                    tableau[i, j] = other.tableau[i, j];

            Array.Copy(other.stock, stock, other.stockCount);
            stockCount = other.stockCount;

            Array.Copy(other.waste, waste, other.wasteCount);
            wasteCount = other.wasteCount;

            recyclesUsed = other.recyclesUsed;
            refillsTriggered = other.refillsTriggered;
        }

        public void Apply(OctagonHintMove m)
        {
            if (m.Type == OctagonHintMove.MoveType.Foundation)
            {
                var c = tableau[m.From, tabLens[m.From] - 1];
                tabLens[m.From]--;
                foundations[m.TargetFoundation] = c.rank;
            }
            else if (m.Type == OctagonHintMove.MoveType.WasteToFoundation)
            {
                var c = waste[wasteCount - 1];
                wasteCount--;
                foundations[m.TargetFoundation] = c.rank;
            }
            else if (m.Type == OctagonHintMove.MoveType.TableauToTableau)
            {
                var c = tableau[m.From, tabLens[m.From] - 1];
                tabLens[m.From]--;
                tableau[m.To, tabLens[m.To]++] = c;
            }
            else if (m.Type == OctagonHintMove.MoveType.WasteToTableau)
            {
                var c = waste[wasteCount - 1];
                wasteCount--;
                tableau[m.To, tabLens[m.To]++] = c;
            }
            else if (m.Type == OctagonHintMove.MoveType.StockDraw)
            {
                waste[wasteCount++] = stock[--stockCount];
            }
            else if (m.Type == OctagonHintMove.MoveType.Recycle)
            {
                for (int i = 0; i < wasteCount; i++) stock[i] = waste[wasteCount - 1 - i];
                stockCount = wasteCount;
                wasteCount = 0;
                recyclesUsed++;
            }

            CheckAutoRefill();
        }

        private void CheckAutoRefill()
        {
            for (int g = 0; g < 4; g++)
            {
                bool isEmpty = true;
                for (int s = 0; s < 5; s++)
                {
                    if (tabLens[g * 5 + s] > 0) { isEmpty = false; break; }
                }

                if (isEmpty && (stockCount > 0 || wasteCount > 0))
                {
                    refillsTriggered++;
                    int added = 0;
                    while (added < 5 && stockCount > 0)
                    {
                        tableau[g * 5 + (4 - added), 0] = stock[--stockCount];
                        tabLens[g * 5 + (4 - added)] = 1;
                        added++;
                    }
                    while (added < 5 && wasteCount > 0)
                    {
                        tableau[g * 5 + (4 - added), 0] = waste[0];
                        tabLens[g * 5 + (4 - added)] = 1;
                        for (int w = 0; w < wasteCount - 1; w++) waste[w] = waste[w + 1];
                        wasteCount--;
                        added++;
                    }
                }
            }
        }
    }

    private class SearchNode
    {
        public FastBoard Board;
        public SearchNode Parent;
        public OctagonHintMove Move;
        public int Depth;

        public SearchNode(FastBoard b, SearchNode p, OctagonHintMove m, int d)
        {
            Board = b; Parent = p; Move = m; Depth = d;
        }
    }

    private class PriorityQueue<T>
    {
        private List<KeyValuePair<T, int>> elements = new List<KeyValuePair<T, int>>(100000);
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

    private Coroutine activeSearchCoroutine;
    private FastBoard scratchBoard; // переиспользуемый черновик для оценки кандидатов без лишних аллокаций

    // --- Для диагностики: id текущего поиска и сколько узлов он успел посетить ---
    // (нужно, чтобы при отмене поиска новым запросом можно было честно залогировать,
    // на каком месте его прервали - это прямой способ увидеть в консоли, действительно
    // ли солверу постоянно не дают досчитать, потому что игрок/фон запускает новый поиск
    // раньше, чем закончился предыдущий).
    private int searchCounter = 0;
    private int activeSearchId = -1;
    private int activeSearchStatesVisited = 0;

    public void FindPath(OctagonPileManager pm, int recyclesUsed, Action<List<OctagonHintMove>> onComplete)
    {
        CancelSearch();
        searchCounter++;
        activeSearchId = searchCounter;
        activeSearchStatesVisited = 0;
        activeSearchCoroutine = StartCoroutine(FindPathAsync(pm, recyclesUsed, onComplete, activeSearchId));
    }

    public void CancelSearch()
    {
        if (activeSearchCoroutine != null)
        {
            if (logSearchProgress)
            {
                Debug.Log($"[HintSolver #{activeSearchId}] Поиск ОТМЕНЁН (StopCoroutine) новым запросом после {activeSearchStatesVisited} посещённых узлов.");
            }
            StopCoroutine(activeSearchCoroutine);
            activeSearchCoroutine = null;
        }
    }

    private IEnumerator FindPathAsync(OctagonPileManager pm, int recyclesUsed, Action<List<OctagonHintMove>> onComplete, int searchId)
    {
        FastBoard root = new FastBoard();
        root.recyclesUsed = (byte)recyclesUsed;

        for (int i = 0; i < 8; i++)
        {
            var top = pm.FoundationPiles[i].GetTopCard();
            root.foundations[i] = top != null ? (byte)top.cardModel.rank : (byte)0;
            // ВАЖНО: раньше GetFoundationIndex вычислял индекс дома по формуле
            // suitBase = suit * 2, что верно только если числовые значения enum Suit
            // идут ровно Spades=0,Hearts=1,Clubs=2,Diamonds=3 в этом же порядке, в котором
            // OctagonDeckManager раскладывает тузы по FoundationPiles. На практике это не
            // так (лог показал: двойка червей отправлялась на дом с трефовым тузом), и
            // формула молча проходила проверку "foundations[suitBase] == rank-1", потому
            // что в начале партии ВСЕ 8 домов равны 1 (туз) независимо от масти. Теперь
            // масть каждого дома читается прямо с реальной доски один раз и используется
            // напрямую вместо арифметики.
            root.foundationSuit[i] = top != null ? (byte)top.cardModel.suit : (byte)255;
        }

        // ВАЖНО: раньше код предполагал, что КАЖДЫЙ дочерний transform слота/колоды/сброса -
        // это карта с компонентом CardController, и брал root.tabLens[idx] прямо из
        // childCount. На практике среди детей может затесаться не-карточный объект
        // (например, декоративный фон/рамка слота) - GetComponent<CardController>()
        // возвращает null, и обращение к card.cardModel валило NullReferenceException
        // прямо в момент чтения доски, до того как солвер вообще успевал начать работу.
        // Теперь такие объекты просто пропускаются, а длина стопки/колоды/сброса
        // считается по фактическому числу найденных карт, а не по childCount.
        for (int g = 0; g < 4; g++)
        {
            for (int s = 0; s < 5; s++)
            {
                int idx = g * 5 + s;
                var slot = pm.TableauGroups[g].Slots[s];
                int childCount = slot.transform.childCount;
                int written = 0;
                for (int j = 0; j < childCount; j++)
                {
                    var card = slot.transform.GetChild(j).GetComponent<CardController>();
                    if (card == null) continue;
                    root.tableau[idx, written] = new CardState { suit = (byte)card.cardModel.suit, rank = (byte)card.cardModel.rank };
                    written++;
                }
                root.tabLens[idx] = written;
            }
        }

        if (pm.StockPile != null)
        {
            int childCount = pm.StockPile.transform.childCount;
            int written = 0;
            for (int i = 0; i < childCount; i++)
            {
                var card = pm.StockPile.transform.GetChild(i).GetComponent<CardController>();
                if (card == null) continue;
                root.stock[written] = new CardState { suit = (byte)card.cardModel.suit, rank = (byte)card.cardModel.rank };
                written++;
            }
            root.stockCount = written;
        }

        if (pm.WastePile != null)
        {
            int childCount = pm.WastePile.transform.childCount;
            int written = 0;
            for (int i = 0; i < childCount; i++)
            {
                var card = pm.WastePile.transform.GetChild(i).GetComponent<CardController>();
                if (card == null) continue;
                root.waste[written] = new CardState { suit = (byte)card.cardModel.suit, rank = (byte)card.cardModel.rank };
                written++;
            }
            root.wasteCount = written;
        }

        if (scratchBoard == null) scratchBoard = new FastBoard();

        var openSet = new PriorityQueue<SearchNode>();
        var closedSet = new HashSet<ulong>(maxSearchNodes);

        int rootFoundSum = 0;
        for (int i = 0; i < 8; i++) rootFoundSum += root.foundations[i];
        int rootTabTotal = 0;
        for (int i = 0; i < 20; i++) rootTabTotal += root.tabLens[i];

        if (logSearchProgress)
        {
            Debug.Log($"[HintSolver #{searchId}] СТАРТ. foundations={rootFoundSum}/104, tableauCards={rootTabTotal}, stock={root.stockCount}, waste={root.wasteCount}, recyclesUsed={root.recyclesUsed}. " +
                      $"Лимиты: maxSearchNodes={maxSearchNodes}, maxChildrenPerNode={maxChildrenPerNode}, maxTotalSearchSeconds={maxTotalSearchSeconds}s.");
        }

        openSet.Enqueue(new SearchNode(root, null, default, 0), CalculateHeuristic(root));

        int statesVisited = 0;
        SearchNode winningNode = null;
        SearchNode bestNode = null; // лучший по foundSum узел из посещённых - на случай если полного пути до победы не найдём вовремя
        string stopReason = null;

        long totalCandidatesGenerated = 0;
        long totalCandidatesKept = 0;
        int bestFoundSum = rootFoundSum;
        double lastLogTime = 0;

        System.Diagnostics.Stopwatch sw = System.Diagnostics.Stopwatch.StartNew();
        System.Diagnostics.Stopwatch totalSw = System.Diagnostics.Stopwatch.StartNew();

        while (openSet.Count > 0)
        {
            if (sw.ElapsedMilliseconds > FRAME_BUDGET_MS)
            {
                yield return null;
                sw.Restart();
            }

            double elapsedSec = totalSw.Elapsed.TotalSeconds;

            // Жёсткий предохранитель по реальному времени - панель "Ищу победный ход..."
            // не должна висеть дольше maxTotalSearchSeconds ни при каких обстоятельствах.
            if (elapsedSec > maxTotalSearchSeconds)
            {
                stopReason = "timeLimit";
                break;
            }

            // НОВОЕ: периодический heartbeat-лог, чтобы было видно, что солвер жив и как
            // быстро он продвигается - вместо того чтобы просто гадать по тишине в консоли.
            if (logSearchProgress && elapsedSec - lastLogTime >= progressLogIntervalSeconds)
            {
                lastLogTime = elapsedSec;
                double rate = statesVisited / Math.Max(0.001, elapsedSec);
                double avgBranch = statesVisited > 0 ? (double)totalCandidatesGenerated / statesVisited : 0;
                Debug.Log($"[HintSolver #{searchId}] {elapsedSec:F1}s | visited={statesVisited} ({rate:F0}/s) | open={openSet.Count} | closed={closedSet.Count} | " +
                          $"best={bestFoundSum}/104 | avgBranch={avgBranch:F1} | kept/gen={totalCandidatesKept}/{totalCandidatesGenerated} | GC0={GC.CollectionCount(0)}");
            }

            var current = openSet.Dequeue();
            statesVisited++;
            activeSearchStatesVisited = statesVisited;

            int curFoundSum = 0;
            for (int i = 0; i < 8; i++) curFoundSum += current.Board.foundations[i];
            if (curFoundSum > bestFoundSum)
            {
                bestFoundSum = curFoundSum;
                bestNode = current;
            }

            // 1. Победа: все восемь баз собраны до конца (ранг = 13)
            bool won = true;
            for (int i = 0; i < 8; i++) if (current.Board.foundations[i] < 13) { won = false; break; }
            if (won)
            {
                winningNode = current;
                stopReason = "won";
                break;
            }

            // 2. Лимит узлов
            if (statesVisited > maxSearchNodes)
            {
                stopReason = "nodeLimit";
                break;
            }

            if (current.Depth > maxDepth) continue;

            ulong hash = current.Board.GetHash();
            if (closedSet.Contains(hash)) continue;
            closedSet.Add(hash);

            var moves = GenerateMoves(current.Board);
            totalCandidatesGenerated += moves.Count;

            // ==========================================================================
            // Раньше здесь для КАЖДОГО кандидата вызывался current.Board.Clone() (полное
            // копирование сетки угольников + до 104 карт колоды + до 104 карт сброса), и
            // только потом проверялось, не встречалось ли такое состояние раньше. Именно
            // это было основной причиной долгих зависаний. Теперь кандидат сперва дёшево
            // применяется на переиспользуемом scratchBoard (без аллокаций), сразу считается
            // хэш результата, и заведомо уже посещённые состояния отбрасываются без
            // клонирования. Клонируется - и кладётся в очередь - только до
            // maxChildrenPerNode лучших по эвристике кандидатов.
            // ==========================================================================
            var candidates = new List<CandidateMove>(moves.Count);
            foreach (var move in moves)
            {
                scratchBoard.CopyFrom(current.Board);
                scratchBoard.Apply(move);

                ulong childHash = scratchBoard.GetHash();
                if (closedSet.Contains(childHash)) continue;

                int hCost = CalculateHeuristic(scratchBoard);
                int fCost = current.Depth + 1 + hCost;
                candidates.Add(new CandidateMove { Move = move, FCost = fCost });
            }

            if (candidates.Count > 1)
                candidates.Sort((a, b) => a.FCost.CompareTo(b.FCost));

            int keep = Mathf.Min(candidates.Count, maxChildrenPerNode);
            totalCandidatesKept += keep;

            // НОВОЕ: подробный разбор первых N раскрытых узлов - чтобы своими глазами
            // увидеть реальное ветвление именно на вашем раскладе, а не в теории.
            if (detailedLogFirstNNodes > 0 && statesVisited <= detailedLogFirstNNodes)
            {
                int minF = candidates.Count > 0 ? candidates[0].FCost : 0;
                int maxFAll = int.MinValue;
                for (int i = 0; i < candidates.Count; i++) if (candidates[i].FCost > maxFAll) maxFAll = candidates[i].FCost;
                Debug.Log($"[HintSolver #{searchId}] Узел #{statesVisited} (глубина={current.Depth}, foundSum={curFoundSum}/104): " +
                          $"сгенерировано ходов={moves.Count}, уникальных после отсева={candidates.Count}, оставлено в очереди={keep}, fCost=[{minF}..{(candidates.Count > 0 ? maxFAll : 0)}].");
            }

            for (int k = 0; k < keep; k++)
            {
                FastBoard nextBoard = current.Board.Clone();
                nextBoard.Apply(candidates[k].Move);
                openSet.Enqueue(new SearchNode(nextBoard, current, candidates[k].Move, current.Depth + 1), candidates[k].FCost);
            }

            // Доска родителя после генерации потомков больше не нужна - без этой строки
            // она (вместе со всеми предками победного пути) держалась бы в памяти всю
            // партию через цепочку Parent.
            current.Board = null;
        }

        if (stopReason == null) stopReason = "exhausted"; // очередь опустела сама - решения не нашлось

        List<OctagonHintMove> path = null;
        bool isPartialPath = false;
        if (winningNode != null && winningNode.Parent != null)
        {
            path = new List<OctagonHintMove>();
            var curr = winningNode;
            while (curr.Parent != null)
            {
                path.Add(curr.Move);
                curr = curr.Parent;
            }
            path.Reverse();
        }
        else if (bestNode != null && bestNode.Parent != null)
        {
            // НОВОЕ: полного доказанного пути до победы найти не успели, но поиск всё же
            // нашёл узел с прогрессом лучше стартового (bestFoundSum > rootFoundSum).
            // Раз исходный расклад гарантированно решаем (проверено солвером на этапе
            // генерации), лучше отдать игроку реальный прогрессирующий ход, чем ничего -
            // это не доказанный до конца путь, но это точно шаг в верном направлении,
            // а не случайный/бесполезный.
            path = new List<OctagonHintMove>();
            var curr = bestNode;
            while (curr.Parent != null)
            {
                path.Add(curr.Move);
                curr = curr.Parent;
            }
            path.Reverse();
            isPartialPath = true;
        }

        if (logSearchProgress)
        {
            double totalElapsed = totalSw.Elapsed.TotalSeconds;
            double avgBranchFinal = statesVisited > 0 ? (double)totalCandidatesGenerated / statesVisited : 0;

            if (stopReason == "won" && path != null)
            {
                Debug.Log($"[HintSolver #{searchId}] РЕШЕНИЕ НАЙДЕНО за {totalElapsed:F2}s, посещено {statesVisited} узлов ({statesVisited / Math.Max(0.001, totalElapsed):F0}/s), путь из {path.Count} ходов.");
                Debug.Log($"[HintSolver #{searchId}] Путь: {FormatPath(path)}");
            }
            else if (isPartialPath)
            {
                string reasonText;
                if (stopReason == "nodeLimit") reasonText = $"достигнут предел узлов ({maxSearchNodes})";
                else if (stopReason == "timeLimit") reasonText = $"достигнут лимит времени ({maxTotalSearchSeconds} сек)";
                else reasonText = "очередь узлов опустела";

                Debug.LogWarning($"[HintSolver #{searchId}] ПОЛНЫЙ путь не найден ({reasonText}), отдаю ЧАСТИЧНЫЙ путь к лучшему найденному прогрессу ({bestFoundSum}/104) за {totalElapsed:F2}s, посещено {statesVisited} узлов, {path.Count} ходов до лучшего узла.");
                Debug.Log($"[HintSolver #{searchId}] Частичный путь: {FormatPath(path)}");
            }
            else
            {
                string reasonText;
                if (stopReason == "nodeLimit") reasonText = $"достигнут предел узлов ({maxSearchNodes})";
                else if (stopReason == "timeLimit") reasonText = $"достигнут лимит времени ({maxTotalSearchSeconds} сек)";
                else if (stopReason == "exhausted") reasonText = "очередь узлов опустела - из этой позиции с текущими лимитами (maxChildrenPerNode/глубина) решения не нашлось";
                else reasonText = stopReason;

                Debug.LogWarning($"[HintSolver #{searchId}] ПОДСКАЗКА НЕ НАЙДЕНА ({reasonText}). За {totalElapsed:F2}s посещено {statesVisited} узлов, лучший результат={bestFoundSum}/104 карт на базах, среднее ветвление={avgBranchFinal:F1}.");
            }
        }

        onComplete?.Invoke(path);
    }

    private string FormatPath(List<OctagonHintMove> path)
    {
        var sb = new StringBuilder();
        for (int i = 0; i < path.Count; i++)
        {
            if (i > 0) sb.Append(" | ");
            sb.Append(FormatMove(path[i]));
        }
        return sb.ToString();
    }

    private string FormatMove(OctagonHintMove m)
    {
        switch (m.Type)
        {
            case OctagonHintMove.MoveType.Foundation: return $"T{m.From}->F{m.TargetFoundation}";
            case OctagonHintMove.MoveType.WasteToFoundation: return $"W->F{m.TargetFoundation}";
            case OctagonHintMove.MoveType.TableauToTableau: return $"T{m.From}->T{m.To}";
            case OctagonHintMove.MoveType.WasteToTableau: return $"W->T{m.To}";
            case OctagonHintMove.MoveType.StockDraw: return "Stock";
            case OctagonHintMove.MoveType.Recycle: return "Recycle";
            default: return m.Type.ToString();
        }
    }

    private int CalculateHeuristic(FastBoard b)
    {
        int h = 0;

        int foundSum = 0;
        for (int i = 0; i < 8; i++) foundSum += b.foundations[i];

        // Основная награда: собранные карты
        h -= foundSum * 100000;

        // Находим минимальное расстояние до следующей нужной карты
        int minDistance = 99999;

        // Карты, которые мы сейчас ждем на базах
        CardState[] needed = new CardState[8];
        for (int i = 0; i < 8; i++)
        {
            needed[i] = new CardState { suit = b.foundationSuit[i], rank = (byte)(b.foundations[i] + 1) };
        }

        // 1. Проверяем открытую карту в сбросе
        if (b.wasteCount > 0)
        {
            var topWaste = b.waste[b.wasteCount - 1];
            for (int i = 0; i < 8; i++)
            {
                if (needed[i].rank <= 13 && topWaste.suit == needed[i].suit && topWaste.rank == needed[i].rank)
                {
                    minDistance = 0;
                    break;
                }
            }
        }

        // 2. Проверяем активные слоты в таблице
        if (minDistance > 0)
        {
            for (int g = 0; g < 4; g++)
            {
                for (int s = 0; s < 5; s++)
                {
                    if (b.tabLens[g * 5 + s] > 0)
                    {
                        var activeCard = b.tableau[g * 5 + s, b.tabLens[g * 5 + s] - 1];
                        for (int i = 0; i < 8; i++)
                        {
                            if (needed[i].rank <= 13 && activeCard.suit == needed[i].suit && activeCard.rank == needed[i].rank)
                            {
                                minDistance = 0;
                                break;
                            }
                        }
                        break; // Дальше в этой группе активных нет
                    }
                }
                if (minDistance == 0) break;
            }
        }

        // 3. Проверяем колоду (сколько раз нужно перелистнуть)
        if (minDistance > 0)
        {
            for (int j = b.stockCount - 1; j >= 0; j--)
            {
                var stockCard = b.stock[j];
                for (int i = 0; i < 8; i++)
                {
                    if (needed[i].rank <= 13 && stockCard.suit == needed[i].suit && stockCard.rank == needed[i].rank)
                    {
                        int draws = (b.stockCount - 1 - j) + 1; // +1 потому что нужно сделать ход StockDraw
                        int dist = draws * 2;
                        if (dist < minDistance) minDistance = dist;
                    }
                }
            }
        }

        // 4. Проверяем скрытые карты в таблице (их нужно раскапывать)
        if (minDistance > 0)
        {
            for (int g = 0; g < 4; g++)
            {
                bool foundActive = false;
                for (int s = 0; s < 5; s++)
                {
                    int len = b.tabLens[g * 5 + s];
                    if (len > 0)
                    {
                        if (!foundActive)
                        {
                            foundActive = true;
                            // Карты под активной
                            for (int j = 0; j < len - 1; j++)
                            {
                                var hiddenCard = b.tableau[g * 5 + s, j];
                                for (int i = 0; i < 8; i++)
                                {
                                    if (needed[i].rank <= 13 && hiddenCard.suit == needed[i].suit && hiddenCard.rank == needed[i].rank)
                                    {
                                        int dist = (len - 1 - j) * 10;
                                        if (dist < minDistance) minDistance = dist;
                                    }
                                }
                            }
                        }
                        else
                        {
                            // Карты в полностью заблокированных слотах
                            for (int j = 0; j < len; j++)
                            {
                                var hiddenCard = b.tableau[g * 5 + s, j];
                                for (int i = 0; i < 8; i++)
                                {
                                    if (needed[i].rank <= 13 && hiddenCard.suit == needed[i].suit && hiddenCard.rank == needed[i].rank)
                                    {
                                        int dist = 20 + (len - j) * 10;
                                        if (dist < minDistance) minDistance = dist;
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }

        // 5. Проверяем глубокий сброс (требуется ресайкл)
        if (minDistance > 0 && b.recyclesUsed < 2)
        {
            for (int j = 0; j < b.wasteCount - 1; j++)
            {
                var wasteCard = b.waste[j];
                for (int i = 0; i < 8; i++)
                {
                    if (needed[i].rank <= 13 && wasteCard.suit == needed[i].suit && wasteCard.rank == needed[i].rank)
                    {
                        int dist = 300 + (b.wasteCount - 1 - j) * 2;
                        if (dist < minDistance) minDistance = dist;
                    }
                }
            }
        }

        // Применяем дистанцию к общему счету
        if (minDistance != 99999)
        {
            h += minDistance;
        }

        // Штраф за ресайклы, чтобы не злоупотреблял
        h += b.recyclesUsed * 1000;

        return h;
    }

    private List<OctagonHintMove> GenerateMoves(FastBoard b)
    {
        var moves = new List<OctagonHintMove>();

        // ИСПРАВЛЕНИЕ: Находим только АКТИВНЫЕ слоты (первая непустая стопка в каждой из 4 групп)
        List<int> activeSlots = new List<int>(4);
        for (int g = 0; g < 4; g++)
        {
            for (int s = 0; s < 5; s++)
            {
                if (b.tabLens[g * 5 + s] > 0)
                {
                    activeSlots.Add(g * 5 + s);
                    break;
                }
            }
        }

        if (b.wasteCount > 0)
        {
            int fIdx = GetFoundationIndex(b, b.waste[b.wasteCount - 1]);
            if (fIdx != -1) moves.Add(new OctagonHintMove { Type = OctagonHintMove.MoveType.WasteToFoundation, TargetFoundation = fIdx });
        }

        // В дом можно класть ТОЛЬКО из активных слотов
        foreach (int from in activeSlots)
        {
            int fIdx = GetFoundationIndex(b, b.tableau[from, b.tabLens[from] - 1]);
            if (fIdx != -1) moves.Add(new OctagonHintMove { Type = OctagonHintMove.MoveType.Foundation, From = from, TargetFoundation = fIdx });
        }

        // Если есть ход на дом - играем только его (сбор на базы всегда в приоритете)
        if (moves.Count > 0) return moves;

        // Таблица -> Таблица: только между активными слотами
        foreach (int from in activeSlots)
        {
            var movingCard = b.tableau[from, b.tabLens[from] - 1];

            foreach (int to in activeSlots)
            {
                if (from == to) continue;

                var targetCard = b.tableau[to, b.tabLens[to] - 1];
                if (targetCard.rank == movingCard.rank + 1)
                {
                    if (b.tabLens[from] > 1)
                    {
                        var revealedCard = b.tableau[from, b.tabLens[from] - 2];
                        if (revealedCard.rank == targetCard.rank)
                        {
                            if (GetFoundationIndex(b, revealedCard) == -1) continue;
                        }
                    }
                    moves.Add(new OctagonHintMove { Type = OctagonHintMove.MoveType.TableauToTableau, From = from, To = to });
                }
            }
        }

        // Сброс -> Таблица: только на активные слоты
        if (b.wasteCount > 0)
        {
            var movingCard = b.waste[b.wasteCount - 1];
            foreach (int to in activeSlots)
            {
                var targetCard = b.tableau[to, b.tabLens[to] - 1];
                if (targetCard.rank == movingCard.rank + 1)
                    moves.Add(new OctagonHintMove { Type = OctagonHintMove.MoveType.WasteToTableau, To = to });
            }
        }

        if (b.stockCount > 0) moves.Add(new OctagonHintMove { Type = OctagonHintMove.MoveType.StockDraw });
        else if (b.wasteCount > 0 && b.recyclesUsed < 2) moves.Add(new OctagonHintMove { Type = OctagonHintMove.MoveType.Recycle });

        return moves;
    }

    private int GetFoundationIndex(FastBoard d, CardState c)
    {
        // Ищем дом ПО ФАКТИЧЕСКИ наблюдаемой масти (d.foundationSuit), а не по формуле
        // suit*2 - формула предполагала конкретный числовой порядок enum Suit, который
        // на практике не совпадает с тем, как реально расставлены FoundationPiles в сцене.
        for (int i = 0; i < 8; i++)
        {
            if (d.foundationSuit[i] == c.suit && d.foundations[i] == c.rank - 1)
                return i;
        }
        return -1;
    }
}