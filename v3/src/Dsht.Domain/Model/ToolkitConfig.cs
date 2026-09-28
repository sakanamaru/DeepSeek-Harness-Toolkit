namespace Dsht.Domain.Model
{
    /// <summary>工具箱配置（纯数据）。字段与默认值逐条对齐 v2.x 的 cfg* 静态字段。</summary>
    public sealed class ToolkitConfig
    {
        public string Lang = "auto";              // zh | en | auto
        public string Host = "127.0.0.1";         // 127.0.0.1 | localhost
        public string Workspace = null;           // 手动工作区；null/空 = 自动探测
        public int KeepBackups = 10;              // 自动类备份保留份数（最小 3）
        public bool CheckUpdate = true;
        public bool CheckDshUpdate = true;
        public string DshVersions = "";           // 历史版本（逗号分隔）
        public string UpdateChannel = "stable";   // stable | rc
        public string CloseAction = "";           // ask | tray | exit | 空（未询问）
        public bool AutoStart = true;

        public ToolkitConfig Copy()
        {
            ToolkitConfig c = new ToolkitConfig();
            c.Lang = Lang; c.Host = Host; c.Workspace = Workspace; c.KeepBackups = KeepBackups;
            c.CheckUpdate = CheckUpdate; c.CheckDshUpdate = CheckDshUpdate; c.DshVersions = DshVersions;
            c.UpdateChannel = UpdateChannel; c.CloseAction = CloseAction; c.AutoStart = AutoStart;
            return c;
        }
    }
}