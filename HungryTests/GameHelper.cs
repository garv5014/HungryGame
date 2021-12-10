using HungryHippos;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HungryTests
{
    public static class GameHelper
    {
        public static string DrawBoard(IEnumerable<Cell> cells)
        {
            var board = new StringBuilder();

            var maxRow = cells.Max(c => c.Location.Row);
            var maxCol = cells.Max(c => c.Location.Column);

            for(int row = 0; row < maxRow; row++)
            {
                for(int col = 0; col < maxCol; col++)
                {
                    var cell = cells.Single(c => c.Location.Row == row && c.Location.Column == col);
                    if(cell.IsPillAvailable)
                    {
                        board.Append("🌯");
                    }
                    else if(cell.OccupiedBy != null)
                    {
                        board.Append(cell.OccupiedBy.Id);
                    }
                    else
                    {
                        board.Append(' ');
                    }
                }
                board.AppendLine();
            }

            return board.ToString();
        }
    }
}
