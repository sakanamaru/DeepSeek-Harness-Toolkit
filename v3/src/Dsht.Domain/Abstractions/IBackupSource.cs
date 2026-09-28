using System;
using System.Collections.Generic;
using Dsht.Domain.Model;

namespace Dsht.Domain.Abstractions
{
    /// <summary>备份来源（平台实现负责读盘）。有效性判定仍由领域层的 BackupPackage 负责。</summary>
    public interface IBackupSource
    {
        /// <summary>备份根（v2.x 语义：状态目录/backup）。</summary>
        string BackupsRoot { get; }

        /// <summary>列出备份根下 dsh-data-* 直接子目录，按名字升序（旧 → 新），与 v2.x 排序一致。</summary>
        List<BackupEntry> ListRaw();

        /// <summary>创建一次备份（Manual/Auto/Pre*）：源目录 → 备份根/dsh-data-&lt;时间戳&gt;&lt;后缀&gt;；
        /// 复制时跳过 node_modules/backup/dsh-data-*/reparse point，被锁文件按 best-effort 跳过；
        /// 成功后执行保留策略（只清自动类）。失败返回 null。</summary>
        BackupResult Create(string sourceDir, BackupKind kind);

        /// <summary>导出备份副本到指定目录（只读源）；返回目标路径，失败返回 null。</summary>
        string Export(string src, string dstDir);

        /// <summary>删除备份目录（保留策略用）；失败静默。</summary>
        void Delete(string dir);

        /// <summary>把备份包**合并恢复**到目标数据根（目标端多余文件不删除），并恢复 _workspace 下的工作区。
        /// 与 v2.x 的 RestoreFromSource 同语义：恢复模式复制失败**如实报错**（不像备份那样跳过被锁文件）；
        /// workspaceRoot 为 null 或不存在时工作区按 v2.x 非交互语义跳过（记入 WorkspacesSkipped）。
        /// 本方法**不做任何安全闸门**：运行中拒绝、恢复前自动备份、--apply 隔离判定都在 CLI 层。</summary>
        RestoreOutcome Restore(string backupDir, string dataRoot, string workspaceRoot);

        /// <summary>读取某目录的快照（名字 + 直接子条目名），供领域层判定有效性。</summary>
        DirSnapshot Snapshot(string dir);

        /// <summary>备份目录定位（下探一层）：返回可用备份目录的完整路径；无法定位返回 null。</summary>
        string Resolve(string path);

        /// <summary>目录总字节数（递归累加文件长度，跳过读不到的项）。</summary>
        long DirSize(string path);

        /// <summary>目录最后写入时间；取不到返回 null（对应 v2.x 的 "(unknown)"）。</summary>
        DateTime? LastWrite(string path);
    }
}