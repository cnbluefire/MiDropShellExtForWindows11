using ShellExtensions;
using ShellExtensions.Helpers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

namespace ShellExt
{
    public class ContextMenu : ExplorerCommand
    {
        private readonly string managerFolder;
        private readonly string icon;

        public ContextMenu(string managerFolder)
        {
            this.managerFolder = managerFolder;
            this.icon = Path.Combine(managerFolder, "XiaomiPcManager.exe,-32512");
        }

        public override string? GetIcon(ShellItemArray shellItems)
        {
            return icon;
        }

        public override string? GetTitle(ShellItemArray shellItems)
        {
            return "使用小米互传发送";
        }

        public override ExplorerCommandState GetState(ShellItemArray shellItems, bool fOkToBeSlow, out bool pending)
        {
            pending = false;
            if (this.ServiceProvider.GetService<ShellExtensions.IContextMenuTypeAccessor>() is { } accessor)
            {
                if (accessor.ContextMenuType == ContextMenuType.ModernContextMenu
                    || accessor.ContextMenuType == ContextMenuType.Unknown)
                {
                    return ExplorerCommandState.ECS_ENABLED;
                }
            }

            return ExplorerCommandState.ECS_HIDDEN;
        }

        public override unsafe void Invoke(ExplorerCommandInvokeEventArgs args)
        {
            var files = args.ShellItems.Select(c => c.FullPath).ToArray();
            DllMain.SendToXiaomiPcManager(files);
        }

    }
}
