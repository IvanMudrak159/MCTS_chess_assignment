﻿using UnityEngine;

namespace Chess
{
    using System;
    using System.Linq;

    class MCTSSearch : ISearch
    {
        public event System.Action<Move> onSearchComplete;

        MoveGenerator moveGenerator;
        MCTSNode rootNode;

        Move bestMove;
        int bestEval;
        bool abortSearch;

        MCTSSettings settings;
        Board board;
        Evaluation evaluation;

        System.Random rand;

        public SearchDiagnostics Diagnostics { get; set; }
        System.Diagnostics.Stopwatch searchStopwatch;

        public MCTSSearch(Board board, MCTSSettings settings)
        {
            this.board = board;
            this.settings = settings;
            evaluation = new Evaluation();
            moveGenerator = new MoveGenerator();
            rand = new System.Random();
        }

        public void StartSearch()
        {
            InitDebugInfo();

            // Initialize search settings
            bestEval = 0;
            bestMove = Move.InvalidMove;

            moveGenerator.promotionsToGenerate = settings.promotionsToSearch;
            abortSearch = false;
            Diagnostics = new SearchDiagnostics();

            rootNode = new MCTSNode(board, null, Move.InvalidMove, board.WhiteToMove, true);

            SearchMoves();

            onSearchComplete?.Invoke(bestMove);

            LogDebugInfo();
        }

        public void EndSearch()
        {
            if (settings.useTimeLimit)
            {
                abortSearch = true;
            }
        }

        void SearchMoves()
        {
            MCTSNode selectedNode = Select(rootNode);
            if (selectedNode.UnexploredMoves.Count > 0)
            {
                Move moveToExplore = selectedNode.UnexploredMoves.First();
                Board newGameState = board.Clone();
                newGameState.MakeMove(moveToExplore);
                MCTSNode childNode = selectedNode.AddChild(moveToExplore, newGameState);
            }
            else
            {
                MCTSNode bestChild = selectedNode.Children.OrderByDescending(UCB1Value).First();
            }
        }

        MCTSNode Select(MCTSNode node)
        {
            while (node.UnexploredMoves.Count == 0 && node.Children.Count > 0)
            {
                node = node.Children.OrderByDescending(UCB1Value).First();
            }
            return node;
        }

        float UCB1Value(MCTSNode node)
        {
            if (node.VisitCount == 0)
                return float.MaxValue;

            float averageValue = node.TotalValue / node.VisitCount;
            averageValue = Mathf.Clamp01(averageValue);
            
            if (!node.IsPlayerMove)
            {
                averageValue = 1 - averageValue;
            }

            float explorationTerm = settings.ExplorationConstant * 
                                    Mathf.Sqrt(Mathf.Log(node.Parent.VisitCount) / node.VisitCount);

            return averageValue + explorationTerm;
        }

        void LogDebugInfo()
        {
            searchStopwatch.Stop();
            UnityEngine.Debug.Log($"Search completed in {searchStopwatch.ElapsedMilliseconds} ms");
        }

        void InitDebugInfo()
        {
            searchStopwatch = System.Diagnostics.Stopwatch.StartNew();
            // Optional
        }
    }
}