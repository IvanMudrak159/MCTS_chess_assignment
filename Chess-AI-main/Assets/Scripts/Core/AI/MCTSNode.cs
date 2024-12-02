namespace Chess
{
    using System.Collections.Generic;

    public class MCTSNode
    {
        public Board GameState { get; private set; }
        public MCTSNode Parent { get; private set; }
        public List<MCTSNode> Children { get; private set; }
        public Move Move { get; private set; }
        public int VisitCount { get; private set; }
        public float TotalValue { get; private set; }
        public List<Move> UnexploredMoves { get; private set; }
        public bool IsPlayerMove { get; private set; }

        public MCTSNode(Board gameState, MCTSNode parent, Move move, bool isPlayerMove)
        {
            MoveGenerator moveGenerator = new MoveGenerator ();
            GameState = gameState;
            Parent = parent;
            Move = move;
            IsPlayerMove = isPlayerMove;
            Children = new List<MCTSNode>();
            UnexploredMoves = moveGenerator.GenerateMoves_DO_NOT_USE(gameState);
            VisitCount = 0;
            TotalValue = 0;
        }

        public MCTSNode AddChild(Move move, Board newGameState)
        {
            var childNode = new MCTSNode(newGameState, this, move, IsPlayerMove);
            Children.Add(childNode);
            UnexploredMoves.Remove(move);
            return childNode;
        }

        public void UpdateStatistics(float simulationResult)
        {
            VisitCount++;
            TotalValue += simulationResult;
        }
    }
}