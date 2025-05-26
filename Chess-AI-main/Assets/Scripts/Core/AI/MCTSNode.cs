using UnityEngine;

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
        public float ValuePercentage {get; private set;}
        
        
        public MCTSNode(Board gameState,Move move, MCTSNode parent = null)
        {
            GameState = gameState;
            Move = move;
            Parent = parent;
            Children = new List<MCTSNode>();
            VisitCount = 0;
            TotalValue = 0;
        }

        public void AddChild(MCTSNode child)
        {
            Children.Add(child);
        }

        public void UpdateStatistics(float simulationResult)
        {
            VisitCount++;
            TotalValue += simulationResult;
            ValuePercentage = TotalValue / VisitCount;
        }
        
        public MCTSNode GetBestChild(float explorationParameter = 1)
        {
            MCTSNode bestChild = null;
            float bestValue = float.MinValue;

            foreach (var child in Children)
            {
                if (child.VisitCount == 0) {
                    return child;
                }
                float uctValue = (child.TotalValue / child.VisitCount) +
                                 explorationParameter * Mathf.Sqrt(Mathf.Log(VisitCount) / (child.VisitCount));

                if (uctValue > bestValue)
                {
                    bestValue = uctValue;
                    bestChild = child;
                }
            }

            return bestChild;
        }
    }
}