using System;
using System.Collections.Generic;
using System.Text;
using System.Drawing;

namespace InternalsViewer.UI.Markers
{
    public class MarkerDefinitionStore
    {
        private static readonly MarkerDefinitionStore store = new MarkerDefinitionStore();
        private Dictionary<MarkerItem, MarkerDefinition> definitions;

        static MarkerDefinitionStore()
        {
        }

        public MarkerDefinitionStore()
        {
            definitions = new Dictionary<MarkerItem, MarkerDefinition>();

            AddDefaultDefinitions();
        }

        public void AddDefaultDefinitions()
        {
            // Data/Index
            definitions.Add(MarkerItem.StatusBitsA, new MarkerDefinition(Color.Red, Color.Gainsboro, MarkerType.Flag, "Status Bits A", true));
            definitions.Add(MarkerItem.StatusBitsB, new MarkerDefinition(Color.Maroon, Color.Gainsboro, MarkerType.Flag, "Status Bits B", true));
            definitions.Add(MarkerItem.FixedLengthSize, new MarkerDefinition(Color.Blue, Color.Gainsboro, MarkerType.Internal, "Fixed Length Size", true));
            definitions.Add(MarkerItem.ColumnCount, new MarkerDefinition(Color.DarkGreen, Color.Gainsboro, MarkerType.Internal, "Column Count", true));
            definitions.Add(MarkerItem.NullBitmap, new MarkerDefinition(Color.DarkOrchid, Color.Gainsboro, MarkerType.Bitmap, "Null Bitmap", true));
            definitions.Add(MarkerItem.VarColumnCount, new MarkerDefinition(Color.AliceBlue, Color.Gainsboro, MarkerType.Internal, "Variable Length Column Count", true));
            definitions.Add(MarkerItem.ColumnOffsetArray, new MarkerDefinition(Color.DarkOrchid, Color.Gainsboro, MarkerType.Bitmap, "Column Offset Array", true));

            // Fields
            definitions.Add(MarkerItem.Dropped, new MarkerDefinition(Color.White, Color.Gray, MarkerType.FixedLengthField, "Dropped", true));
            definitions.Add(MarkerItem.FixedLengthField, new MarkerDefinition(Color.Gray, Color.LemonChiffon, MarkerType.FixedLengthField, "[Fixed length field]", true));
            definitions.Add(MarkerItem.FixedLengthFieldAlt, new MarkerDefinition(Color.Gray, Color.PaleGoldenrod, MarkerType.FixedLengthField, "[Fixed length field (alternate)]", true));
            definitions.Add(MarkerItem.Null, new MarkerDefinition(Color.White, Color.White, MarkerType.FixedLengthField, "(Null)", true));
            definitions.Add(MarkerItem.Uniqueifier, new MarkerDefinition(Color.FromArgb(64, 64, 64), Color.PaleGreen, MarkerType.Internal, "Uniqueifier", true));
            definitions.Add(MarkerItem.VarLengthField, new MarkerDefinition(Color.Gray, Color.LemonChiffon, MarkerType.Internal, "[Variable length field]", true));
            definitions.Add(MarkerItem.VarLengthFieldAlt, new MarkerDefinition(Color.Gray, Color.PaleGoldenrod, MarkerType.Internal, "[Variable length field (alternate)]", true));
        }

        public MarkerDefinition GetItem(MarkerItem item)
        {
            return definitions[item];
        }

        public static MarkerDefinitionStore Store
        {
            get { return store; }
        }
    }
}
