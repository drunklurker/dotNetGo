using System;
using System.Collections.Generic;
using System.Net.Configuration;
using System.Runtime.CompilerServices;
using System.Text;
using System.Xml.XPath;

namespace dotNetGo
{
    class Board
    {
        public enum GameState
        {
            GameIsNotOver,
            BlackSurrendered,
            WhiteSurrendered,
            DoublePass
        }
        //for printing purposes only
        private const string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
        //board size
        private static readonly int _size = GameParameters.boardSize;
        private static readonly double _komi = GameParameters.komi;
        //cardinal directions
        public static readonly int[,] CardinalDirections = {{-1, 0}, {0, 1}, {1, 0}, {0, -1}};
        public static readonly int[,] DiagonalDirections = {{-1, 1}, {1, 1}, {1, -1}, {-1, -1}};
        public const int DirectionCount = 4;
        private int _passes = 0;
        //turn number - needed for simulations
        public int TurnNumber { get; private set; }

        public int Passes
        {
            get
            {
                return _passes;
            }
            private set
            {
                _passes = value;
                if (_passes >= 2)
                    State = GameState.DoublePass;
            }
        }

        //prisoner count
        public int BlackCaptured { get; private set; }
        public int WhiteCaptured { get; private set; }
        public byte ActivePlayer { get; private set; }

        public GameState State { get; private set; }

        public byte OppositePlayer
        {
            get { return (byte)(3 - ActivePlayer); }
        }

        private readonly bool[,] _visited = new bool[_size,_size];
        private readonly byte[,] _buffer = new byte[_size,_size];
        //needed for ko checks
        private readonly byte[,] _lastPosition = new byte[_size,_size];

        readonly byte[,] _board = new byte[_size, _size];

        public byte this[int i, int j]
        {
            get { return _board[i, j]; }
            set { _board[i, j] = value; }
        }

        public byte this[Move m]
        {
            get { return _board[m.row, m.column]; }
            set { _board[m.row, m.column] = value; }
        }

        public byte[,] GetBoard()
        {
            return _board;
        }
        public Board()
        {
            BlackCaptured = 0;
            WhiteCaptured = 0;
            ActivePlayer = 1;
            TurnNumber = 1;
        }
        public void CopyState(Board b)
        {
            BlackCaptured = b.BlackCaptured;
            WhiteCaptured = b.WhiteCaptured;
            TurnNumber = b.TurnNumber;
            Passes = b.Passes;
            ActivePlayer = b.ActivePlayer;
            State = b.State;
            for (int i = 0; i < _size; i++)
                for (int j = 0; j < _size; j++)
                {
                    _visited[i, j] = b._visited[i, j];
                    _board[i, j] = b[i, j];
                    _lastPosition[i, j] = b._lastPosition[i, j];
                }
        }

        public bool PlaceStone(Move move)
        {
            if (State != GameState.GameIsNotOver)
                return false;
            if (move.row == -1 && move.column == -1)
            {
                Pass();
                return true;
            }
            //check if the move is on the board
            if (IsOnBoard(move) == false)
            {
                return false;
            }
            //check if the intersection is already occupied
            if (_board[move.row, move.column] != 0)
            {
                return false;
            }
            //check if the move is forbidden because of the Ko rule
            if (IsKo(move.row, move.column))
            {
                return false;
            }
            Array.Copy(_board, _buffer, _board.Length);
            this[move] = ActivePlayer;

            //if there is an enemy dragon nearby, it won't contain newly-placed stone - have to check each one individually
            Array.Clear(_visited, 0, _visited.Length);
            bool isSuicide = true; //для более быстрого определения возможности хода. Если рядом с новым камнем есть свободное пересечение, то это точно не суицид
            for (int i = 0; i < DirectionCount; i++) //first check opponent's dragons
            {
                int prisoners;
                int testRow = move.row + CardinalDirections[i, 0];
                int testCol = move.column + CardinalDirections[i, 1];
                //если клетка находится за доской или на ней стоит союзный камень - пропускаем. проверка союзных камней будет позже
                //if an intersection is outside the board or has allied stone - skip it for now. allied checks will come later
                if (IsOnBoard(testRow, testCol) == false || _board[testRow, testCol] == ActivePlayer)
                    continue;
                else if (_board[testRow, testCol] == 0)
                    //if a neighbouring intersection is empty, then the new stone will definitely have at least 1 liberty
                {
                    isSuicide = false;
                    continue;
                }
                if (_board[testRow, testCol] == OppositePlayer)
                {
                    if (_visited[testRow, testCol] == true)
                        continue;
                    else
                    {
                        Array.Clear(_visited, 0, _visited.Length);
                        if (CountDragonLiberties(testRow, testCol) == 0)
                        {
                            prisoners = RemoveDragon(testRow, testCol);
                            switch (ActivePlayer)
                            {
                                case 1:
                                    WhiteCaptured += prisoners;
                                    break;
                                case 2:
                                    break;
                            }
                        }
                    }
                }
            }
            Array.Clear(_visited, 0, _visited.Length);
            //если рядом с новым камнем есть дракон, то он включает в себя и этот камень
            //if there is a nearby friendly dragon, it will contain newly-placed stone
            if (isSuicide == true || CountDragonLiberties(move.row, move.column) == 0)
            {
                this[move] = 0;
                return false;
            }

            ActivePlayer = OppositePlayer;
            //TODO: possibly add ko checks
            Passes = 0;
            TurnNumber++;
            Array.Copy(_buffer, _lastPosition, _lastPosition.Length);
            return true;
        }
        

        int RemoveDragon(int row, int col)
        {
            if (IsOnBoard(row, col) == false || IsFree(row, col) == true)
                return 0;
            if (_board[row, col] == ActivePlayer)
                //if we encounter an active's player stone - skip it, because suicide is forbidden
                return 0;

            //AT THIS POINT, we know that an intersection is on board and it contains an opponent's stone
            int result = 1;
            _board[row, col] = 0;
            for (int i = 0; i < DirectionCount; i++)
            {
                int testRow = row + CardinalDirections[i, 0];
                int testCol = col + CardinalDirections[i, 1];
                result += RemoveDragon(testRow, testCol);
            }
            return result;
        }

        int RemoveDragon(Move m)
        {
            return RemoveDragon(m.row, m.column);
        }
        
        public void Pass()
        {
            ActivePlayer = OppositePlayer;
            TurnNumber++;
            Passes++;
        }

        //counts liberties of a stone group that include the stone at coordinates of m
        //return values:
        //-1 if m is empty space, or if this space has already been visited (to remove redundant dragon checks)
        //number of liberties of the dragon otherwise
        private int CountDragonLiberties(int row, int col)
        {
            if (_board[row, col] == 0 || _visited[row, col] == true)
                return -1;
            _visited[row, col] = true;
            int result = 0;
            for (int i = 0; i < DirectionCount; i++)
            {
                int testRow = row + CardinalDirections[i, 0];
                int testCol = col + CardinalDirections[i, 1];
                if (IsOnBoard(testRow, testCol) == false || _visited[testRow, testCol] == true)
                    continue;
                if (IsFree(testRow, testCol))
                {
                    result++;
                    _visited[testRow, testCol] = true;
                }
                else if (_board[testRow, testCol] == _board[row, col])
                    result += CountDragonLiberties(testRow, testCol);
            }
            return result;
        }
        public bool IsFree(int row, int col)
        {
            return IsOnBoard(row, col) && _board[row, col] == 0;
        }
        public bool IsFree(Move m)
        {
            return IsOnBoard(m) && _board[m.row, m.column] == 0;
        }
        public bool IsOnBoard(int row, int col)
        {
            return row >= 0 & row < _size && col >= 0 && col < _size;
        }
        public bool IsOnBoard(Move m)
        {
            return m.row >= 0 & m.row < _size && m.column >= 0 && m.column < _size;
        }
        public int DetermineWinner(out double blackScore, out double whiteScore)
        {
            whiteScore = 0;
            blackScore = 0;

            if (State == GameState.BlackSurrendered)
                return 2;
            if (State == GameState.WhiteSurrendered)
                return 1;
            int[] scores = new int[2]{0,0};
            if (IsGameOver() == false)
                return 0;
            for (int player = 1; player < 3; player++)
            {
                for (int i = 0; i < _size; i++)
                {
                    for (int j = 0; j < _size; j++)
                    {
                        if (_board[i, j] == player)
                            scores[player - 1]++;
                        else
                        {
                            int eyeOwner;
                            if (IsEye(i, j, out eyeOwner))
                            {
                                if (eyeOwner == player)
                                    scores[player - 1]++;
                            }
                        }
                    }
                }
            }
            blackScore = scores[0];
            whiteScore = scores[1] + _komi;
            if (blackScore > whiteScore)
                return 1;
            else return 2;
        }

        public void Surrender()
        {
            switch (ActivePlayer)
            {
                case 1:
                    State = GameState.BlackSurrendered;
                    break;
                case 2:
                    State = GameState.WhiteSurrendered;
                    break;
            }
        }

        //checks whether the game is over
        //returns true if there all empty spaces are 1-space eyes
        //returns false when there are potential moves left
        public bool IsGameOver()
        {
            if (State != GameState.GameIsNotOver)
                return true;
            //now check if all existing empty intersection are eyes. If they are - the game is definitely over
            //TODO: possibly add seki checks
            int owner;
            for (int i = 0; i < _size; i++)
            {
                for (int j = 0; j < _size; j++)
                {
                    if (IsEye(i, j, out owner) == false)
                        return false;
                }
            }
            return true;
        }

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder("  ");
            for (int i = 0; i < _size; i++)
                sb.Append(i.ToString());
            sb.AppendLine();
            for (int i = 0; i < _size; i++)
            {
                sb.Append(String.Format("{0} ", i));
                for (int j = 0; j < _size; j++)
                {
                    switch (_board[i, j])
                    {
                        case 1:
                            sb.Append("b");
                            break;
                        case 2:
                            sb.Append("w");
                            break;
                        default:
                            sb.Append(".");
                            break;
                    }
                }
                sb.AppendLine();
            }
//            Program.ToStringSpan += DateTime.Now - start;
            return sb.ToString();
        }

        //return values:
        //0 - not an eye
        //1 - black eye
        //2 - white eye
        //an intersection is an eye when all immediately surrounding stones belong to the same dragon - also fixes false eyes
        public bool IsEye(int row, int col, out int owner)
        {
            owner = 0;
            if (IsOnBoard(row, col) == false || IsFree(row, col) == false)
            {
                return false;
            }
            int black = 0;
            int white = 0;
            for (int i = 0; i < DirectionCount; i++)
            {
                int testRow = row + CardinalDirections[i, 0];
                int testCol = col + CardinalDirections[i, 1];
                if (IsOnBoard(testRow, testCol) == false)
                {
                    black++;
                    white++;
                    continue;
                }
                switch (_board[testRow, testCol])
                {
                    case 1:
                        black++;
                        break;
                    case 2:
                        white++;
                        break;
                }
            }
            if (black < 4 && white < 4)
                return false;
            for (int i = 0; i < DirectionCount; i++)
            {
                int testRow = row + DiagonalDirections[i, 0];
                int testCol = col + DiagonalDirections[i, 1];
                if (IsOnBoard(testRow, testCol) == false)
                {
                    black++;
                    white++;
                    continue;
                }
                switch (_board[testRow, testCol])
                {
                    case 1:
                        black++;
                        break;
                    case 2:
                        white++;
                        break;
                }
            }
            if (black >= 7)
            {
                owner = 1;
                return true;
            }
            if (white >= 7)
            {
                owner = 2;
                return true;
            }
            return false;
//            Array.Clear(_visited, 0, _visited.Length);
//            bool playerMet = false;
//            for (int i = 0; i < DirectionCount; i++)
//            {
//                int testRow = row + CardinalDirections[i, 0];
//                int testCol = col + CardinalDirections[i, 1];
//                if (IsOnBoard(testRow, testCol) == false)
//                {
//                    if (IsFree(testRow, testCol) == true)
//                        return false;
//                    else continue;
//                }
//                //AT THIS POINT: tested intersection is NOT empty and is on board
//                if (playerMet == false)
//                {
//                    CountDragonLiberties(testRow, testCol);
//                    owner = _board[testRow, testCol];
//                    playerMet = true;
//                }
//                else
//                {
//                    if (_visited[testRow, testCol] == false)
//                    {
//                        owner = 0;
//                        return false;
//                    }
//                }
//            }
//            return true;
        }
        public bool IsEye(Move move, out int owner) //false eyes fixed
        {
            return IsEye(move.row, move.column, out owner);
        }

        public bool IsKo(int row, int col)
        {
            int differences = 0;
            if (_lastPosition[row, col] != ActivePlayer)
                return false;
            for (int i = 0; i < _size; i++)
            {
                for (int j = 0; j < _size; j++)
                {
                    if (_board[i, j] != _lastPosition[i, j])
                        differences++;
                }
                if (differences > 2)
                    return false;
            }
            //AT THIS POINT: we know that one of the differences is the current point, and there are 2 of them in total
            //the other one MUST be adjacent (cannot be otherwise), so it is Ko and the move is forbidden
            return true;
        }
    }
}