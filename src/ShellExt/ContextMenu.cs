using ShellExtensions;
using ShellExtensions.Helpers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

namespace ShellExt
{
    public class ContextMenu : ExplorerCommand
    {
        private readonly string target;
        private readonly string icon;
        private readonly string title;

        public ContextMenu(string target, string icon, string title)
        {
            this.target = target;
            this.icon = icon;
            this.title = title;
        }

        public override string? GetIcon(ShellItemArray shellItems)
        {
            return icon;
        }

        public override string? GetTitle(ShellItemArray shellItems)
        {
            return title;
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
            if (files != null && files.Length > 0)
            {
                DllMain.StartShare(target, files);
            }
        }

    }
}
