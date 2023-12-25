using System;
using System.Collections.Generic;
using Sandbox.ModAPI;
using Sandbox.Game;
using VRage.ModAPI;
using VRage.Game.ModAPI;

namespace RadarBlock
{
	public class LCDManager
	{
		public static void Add(IMyEntity parent, string lcdName, string data, bool append = true)
		{
			IMyTextPanel panel = FindGridPanel(parent, lcdName);

			if (panel != null)
			{
                bool firstEntry = false;
                if (panel.GetText() == " ")
                    firstEntry = true;

				panel.WriteText(data + "\n", append && !firstEntry);
				//panel.WritePrivateText(data + "\n", append);
			}
		}

		public static void Clear(IMyEntity parent, string lcdName)
		{
			IMyTextPanel panel = FindGridPanel(parent, lcdName);

            if (panel != null)
			{
				panel.WriteText(" ");
                //panel.WritePrivateText(" ");
            }
		}

		public static IMyTextPanel FindGridPanel(IMyEntity parent, string lcdName)
		{

            IMyTextPanel result = null;
            try
            {
                
                if (string.IsNullOrEmpty(lcdName)) return result;

                if (!(parent is IMyCubeGrid))
                    return result;

                IMyCubeGrid grid = (IMyCubeGrid)parent;
                List<IMySlimBlock> blocks = new List<IMySlimBlock>();
                grid.GetBlocks(blocks);
                foreach (IMySlimBlock block in blocks)
                {
                    if (block.FatBlock == null)
                        continue;

                    if (!(block.FatBlock is IMyTextPanel))
                        continue;

                    if (((IMyTextPanel)block.FatBlock).CustomName.ToLower() != lcdName.ToLower())
                        continue;

                    result = (IMyTextPanel)block.FatBlock;
                    break;
                }
            }
            catch (Exception exc)
            {
                VRage.Utils.MyLog.Default.WriteLineAndConsole($"Radar Find LCD Failed {exc}");
            }

            return result;
        }
	}
}
