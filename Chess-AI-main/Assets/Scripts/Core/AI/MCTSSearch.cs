namespace Chess
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using UnityEngine;
    using static System.Math;
    class MCTSSearch : ISearch
    {
        enum KingDead
        {
            None,
            Black,
            White,
        }

        public event System.Action<Move> onSearchComplete;

        MoveGenerator moveGenerator;

        MCTSNode rootSearchNode;

        Move bestMove;
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

            bestMove = Move.InvalidMove;

            moveGenerator.promotionsToGenerate = settings.promotionsToSearch;
            abortSearch = false;
            Diagnostics = new SearchDiagnostics();

            rootSearchNode = new MCTSNode(board, Move.InvalidMove);
            SearchMoves();

            onSearchComplete?.Invoke(bestMove);

            if (!settings.useThreading)
            {
                LogDebugInfo();
            }
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
            ExpandNode(rootSearchNode, true);
            searchStopwatch.Start();
            int numOfPlayouts = 0;
            while (searchStopwatch.ElapsedMilliseconds < settings.searchTimeMillis && numOfPlayouts < settings.maxNumOfPlayouts && !abortSearch)
            {
                MCTSNode selectedNode = SelectBestNode();
                if (selectedNode.VisitCount > 0)
                {
                    selectedNode = ExpandNode(selectedNode, false);
                }
                float simulationResult = Simulate(selectedNode);
                Backpropagate(selectedNode,1 - simulationResult);

                numOfPlayouts++;

            }
            bestMove = rootSearchNode.Children
                .OrderByDescending(child => child.TotalValue)
                .First().Move;
            
            List<MCTSNode> nodes = rootSearchNode.Children
                .OrderByDescending(child => child.TotalValue).ToList();
            
            searchStopwatch.Stop();
        }

        MCTSNode SelectBestNode()
        {
            var node = rootSearchNode;
            while (node.Children.Count > 0)
            {
                node = node.GetBestChild();
            }
            return node;
        }

        MCTSNode ExpandNode(MCTSNode nodeToExpand, bool isRoot)
        {
            List<Move> unexploredMoves = moveGenerator.GenerateMoves(nodeToExpand.GameState, isRoot);
            unexploredMoves.Reverse();
            MCTSNode node = nodeToExpand;
            foreach (Move move in unexploredMoves)
            {
                Board gameState = nodeToExpand.GameState.Clone();
                gameState.MakeMove(move);
                node = new MCTSNode(gameState, move, nodeToExpand);
                nodeToExpand.AddChild(node);
            }
            return node;
        }

        float Simulate(MCTSNode nodeForSim)
        {
            var simState = nodeForSim.GameState.GetLightweightClone();
            bool whiteTurn = nodeForSim.GameState.WhiteToMove;

            int playoutDepth = 0;
            while (playoutDepth < settings.playoutDepthLimit && GetKingCaptured(simState) == KingDead.None)
            {
                var possibleMoves = moveGenerator.GetSimMoves(simState, whiteTurn);
                SimMove randomMove = possibleMoves[rand.Next(possibleMoves.Count)];

                simState = ApplySimMove(simState, randomMove);
                whiteTurn = !whiteTurn;
                playoutDepth++;
            }

            KingDead res = GetKingCaptured(simState);
            if (res == KingDead.None)
            {
                return evaluation.EvaluateSimBoard(simState, nodeForSim.GameState.WhiteToMove);
            }
            return EvaluateKingCaptured(res, nodeForSim.GameState.WhiteToMove);
        }

        void Backpropagate(MCTSNode simNode, float result)
        {
            while (simNode != null)
            {
                simNode.UpdateStatistics(result);
                result = 1 - result;
                simNode = simNode.Parent;
            }
        }

        KingDead GetKingCaptured(SimPiece[,] simState)
        {
            bool whiteAlive = false;
            bool blackAlive = false;

            for (int row = 0; row < simState.GetLength(0); row++)
            {
                for (int col = 0; col < simState.GetLength(1); col++)
                {
                    SimPiece piece = simState[row, col];
                    if (piece != null && piece.type == SimPieceType.King)
                    {
                        if (simState[row, col].team)
                        {
                            whiteAlive = true;
                        }
                        else
                        {
                            blackAlive = true;
                        }
                    }
                    if (whiteAlive && blackAlive) return KingDead.None;
                }
            }
            return !blackAlive ? KingDead.Black : KingDead.White;
        }

        float EvaluateKingCaptured(KingDead deadKing, bool whiteToMove)
        {
            int res;
            if (whiteToMove)
            {
                res = deadKing == KingDead.Black ? 1 : 0;
            }
            else
            {
                res = deadKing == KingDead.White ? 1 : 0;
            }
            return res;
        }

        private SimPiece[,] ApplySimMove(SimPiece[,] state, SimMove move)
        {
            state[move.endCoord1, move.endCoord2] = state[move.startCoord1, move.startCoord2];
            state[move.startCoord1, move.startCoord2] = null;
            return state;
        }

        void LogDebugInfo()
        {
        }

        void InitDebugInfo()
        {
            searchStopwatch = System.Diagnostics.Stopwatch.StartNew();
        }
    }
}