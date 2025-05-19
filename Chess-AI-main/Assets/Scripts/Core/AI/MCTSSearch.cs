using System.Collections.Generic;
using UnityEngine;


namespace Chess
{
    using System.Linq;

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

            rootNode = new MCTSNode(board, null, Move.InvalidMove, true, true);

            int iterations = 0;
            while (!abortSearch && (iterations < settings.maxNumOfPlayouts || settings.useTimeLimit))
            {
                SearchMoves();
                iterations++;
                
                if (settings.useTimeLimit && searchStopwatch.ElapsedMilliseconds >= settings.searchTimeMillis)
                {
                    abortSearch = true;
                }
            }
            
            bestMove = SelectBestMove();

            onSearchComplete?.Invoke(bestMove);

            LogDebugInfo();
        }

        private Move SelectBestMove()
        {
            if (rootNode.Children.Count == 0)
                return Move.InvalidMove;
                
            MCTSNode bestChild = rootNode.Children
                .OrderByDescending(n => n.VisitCount == 0 ? float.NegativeInfinity : n.TotalValue / n.VisitCount)
                .First();
            List<MCTSNode> children = rootNode.Children.OrderByDescending(n => n.TotalValue / n.VisitCount).ToList();
            return bestChild.Move;
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
            MCTSNode selectedNode = Expand();
            if (selectedNode == null)
            {
                Debug.LogError("selectedNode is null");
                return;
            }
            float simulationResult = Simulate(selectedNode);
            Backpropagate(selectedNode, simulationResult);
        }

        private MCTSNode Expand()
        {
            MCTSNode node = rootNode;

            while (node.UnexploredMoves.Count == 0 && node.Children.Count > 0)
            {
                node = node.Children.OrderByDescending(UCB1Value).First();
            }

            if (node.UnexploredMoves.Count > 0)
            {
                Move moveToExplore = node.UnexploredMoves.Last();
                node.UnexploredMoves.Remove(moveToExplore);

                Board newGameState = node.GameState.Clone();
                newGameState.MakeMove(moveToExplore);
                MCTSNode child = node.AddChild(moveToExplore, newGameState);

                return child;
            }
            return node;
        }

        private float Simulate(MCTSNode node)
        {       
            SimPiece[,] simState = node.GameState.GetLightweightClone();
            bool currentPlayer = node.GameState.WhiteToMove;
            //List<SimMove> moves = new List<SimMove>();
            KingDead deadKing = KingDead.None;
            for (int depth = 0; depth < settings.playoutDepthLimit; depth++)
            {
                var simMoves = moveGenerator.GetSimMoves(simState, currentPlayer);
                
                if (simMoves.Count == 0)
                {
                    break;
                }
                SimMove randomMove = simMoves[rand.Next(simMoves.Count)];
                //moves.Add(randomMove);
                simState = ApplySimMove(simState, randomMove);
                
                deadKing = GetKingCaptured(simState);
                
                if(deadKing == KingDead.Black && currentPlayer) break;
                if(deadKing == KingDead.White && !currentPlayer) break;
                
                currentPlayer = !currentPlayer;
            }
            
            if (deadKing == KingDead.None)
            {
                return Mathf.Clamp01(evaluation.EvaluateSimBoard(simState, !node.GameState.WhiteToMove));
            }
            return EvaluateKingCaptured(deadKing, !node.GameState.WhiteToMove);
        }
        
        private SimPiece[,] ApplySimMove(SimPiece[,] state, SimMove move)
        {
            state[move.endCoord1, move.endCoord2] = state[move.startCoord1, move.startCoord2];
            state[move.startCoord1, move.startCoord2] = null;
            return state;
        }

        private void Backpropagate(MCTSNode node, float simulationResult)
        {
            MCTSNode currentNode = node;
            while (currentNode != null)
            {
                currentNode.UpdateStatistics(simulationResult);
                simulationResult = 1 - simulationResult;
                currentNode = currentNode.Parent;
            }
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
        
        void LogDebugInfo()
        {
            searchStopwatch.Stop();
            Debug.Log($"Search completed in {searchStopwatch.ElapsedMilliseconds} ms");
            Debug.Log($"Best move found: {bestMove}");
        }

        void InitDebugInfo()
        {
            searchStopwatch = System.Diagnostics.Stopwatch.StartNew();
            // Optional
        }
    }
}
