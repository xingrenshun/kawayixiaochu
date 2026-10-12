using System;

namespace GameGrid
{
    public enum EGridState
    {
        Empty,
        Obstacle,
        Busy
    }

    public struct GridState : IEquatable<GridState>
    {
        public int x, y;
        public EGridState state;

        public bool IsObstacleOrBusy()
        {
            return state == EGridState.Obstacle || state == EGridState.Busy;
        }
        public bool IsObstacle()
        {
            return state == EGridState.Obstacle;
        }
        public bool IsObstacleOrBusy(GridState ignore)
        {
            return (state == EGridState.Obstacle || state == EGridState.Busy) && this != ignore;
        }
        public override bool Equals(object other)
        {
            if (other is not GridState) return false;
            var otherState = (GridState)other;
            return x == otherState.x && y == otherState.y;
        }

        public static bool operator ==(GridState a, GridState b)
        {
            return a.x == b.x && a.y == b.y;
        }

        public static bool operator !=(GridState a, GridState b)
        {
            return a.x != b.x || a.y != b.y;
        }
        public override int GetHashCode()
        {
            return x.GetHashCode() ^ y.GetHashCode();
        }

        public bool Equals(GridState other)
        {
            return x == other.x && y == other.y && state == other.state;
        }

        public override string ToString()
        {
            return $"(x:{x},y:{y},state:{state})";
        }
    }
}