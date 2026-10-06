using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using System.Diagnostics;

public class OctagonGenerator : BaseGenerator
{
    public override GameType GameType => GameType.Octagon;

    [Header("Optimization")]
    [Range(1, 16)]
    public float frameBudgetMs = 8.0f; // ����� ������� �� ����, ����� ���� �� �������

    public override IEnumerator GenerateDeal(Difficulty difficulty, int param, Action<Deal, DealMetrics> onComplete)
    {
        Deal validDeal = null;
        bool lastResultSolved = false;
        int attempts = 0;
        Stopwatch sw = new Stopwatch();

        UnityEngine.Debug.Log($"[OctagonGen] Starting generation (Solvable Only)...");

        while (validDeal == null)
        {
            attempts++;

            if (sw.ElapsedMilliseconds > frameBudgetMs) { yield return null; sw.Restart(); }
            else if (!sw.IsRunning) sw.Start();

            // 1. ������� ��������� ������� 
            Deal candidate = CreateRandomOctagonDeal();

            // 2. ��������� ��������
            OctagonSolver.ExtendedSolverResult result = new OctagonSolver.ExtendedSolverResult();
            yield return StartCoroutine(OctagonSolver.SolveAsync(candidate, frameBudgetMs, result));

            sw.Restart();

            if (result.IsSolved)
            {
                UnityEngine.Debug.Log($"<color=green>[OctagonGen] SUCCESS! Found solvable deal. Attempts: {attempts}.</color>");
                validDeal = candidate;
                lastResultSolved = true;
            }

            // BUG FIX: this used to silently ship the last candidate as-is after 50
            // failed attempts, WITHOUT it ever being proven solvable, while still
            // reporting DealMetrics.Solved = true unconditionally below - so a player
            // could receive a genuinely unsolvable deal that claimed to be solvable.
            // Raised the safety-valve threshold a lot (now that MAX_DEPTH/MAX_STATES in
            // OctagonSolver were themselves the reason many truly-solvable deals were
            // failing to be proven, most attempts should now succeed well before this
            // fires) and made the eventual fallback loud and honest about what happened.
            if (attempts >= 300)
            {
                UnityEngine.Debug.LogError($"[OctagonGen] Reached {attempts} attempts without a solver-proven deal. Shipping the last candidate UNVALIDATED to avoid freezing generation - it may not actually be solvable. Investigate solver limits/heuristic if this happens often.");
                validDeal = candidate;
                lastResultSolved = false;
            }
        }

        DealMetrics metrics = new DealMetrics { Solved = lastResultSolved };
        onComplete?.Invoke(validDeal, metrics);
    }

    private Deal CreateRandomOctagonDeal()
    {
        List<CardModel> deck = new List<CardModel>();

        // �������� 2 ������ ������ (104 �����)
        for (int i = 0; i < 2; i++)
        {
            foreach (Suit s in Enum.GetValues(typeof(Suit)))
            {
                for (int r = 1; r <= 13; r++) deck.Add(new CardModel(s, r));
            }
        }

        // ��������� 8 ����� (��� ������������� ��������� � ���)
        for (int i = 0; i < 8; i++)
        {
            var ace = deck.First(c => c.rank == 1);
            deck.Remove(ace);
        }

        // ������������ ���������� 96 ����
        Shuffle(deck);

        Deal deal = new Deal();
        deal.tableau = new List<List<CardInstance>>();

        int cardIndex = 0;

        // ������� 20 ���� �� ���� (4 ������ �� 5 ������)
        for (int g = 0; g < 4; g++)
        {
            List<CardInstance> groupCards = new List<CardInstance>();
            for (int s = 0; s < 5; s++)
            {
                // � ��������������� ��� ����� �� ����� �������
                groupCards.Add(new CardInstance(deck[cardIndex++], true));
            }
            deal.tableau.Add(groupCards);
        }

        deal.stock = new Stack<CardInstance>();

        // ���������� 76 ���� ������ � ������ (�������� �����)
        for (int i = cardIndex; i < deck.Count; i++)
        {
            deal.stock.Push(new CardInstance(deck[i], false));
        }

        return deal;
    }

    private void Shuffle(List<CardModel> list)
    {
        System.Random rng = new System.Random();
        int n = list.Count;
        while (n > 1)
        {
            n--;
            int k = rng.Next(n + 1);
            var temp = list[k];
            list[k] = list[n];
            list[n] = temp;
        }
    }
}