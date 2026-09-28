/**
 * dsh-minato-bridge —— **纯逻辑**部分（零依赖：只用 node 内置模块，**不 import 任何 dsh 包**）。
 *
 * 这样拆分的原因与 C# 侧"领域层 / 平台层"一致：把不依赖宿主的逻辑单独放，就能用普通 `node` 直接自测，
 * 不需要装 dsh、也不需要它的 peer 依赖（`@deepseek-ai/schemastery` 由宿主提供，只在 index.js 里用）。
 */
import fs from "node:fs";
import path from "node:path";

/** 快照格式版本（工具箱侧 Dsht.Domain.Services.SessionStats.SnapshotFormatVersion 必须一致）。 */
export const SNAPSHOT_FORMAT_VERSION = 2;

/** 默认快照路径：<DSH_HOME>/shio-bridge/sessions.json（DSH_HOME 未设则退回 ~/.dsh）。 */
export function defaultOutFile(env) {
	const home = (env && env.DSH_HOME) || "";
	const base =
		home && home.trim().length > 0
			? home.trim()
			: path.join(process.env.HOME || process.env.USERPROFILE || ".", ".dsh");
	return path.join(base, "shio-bridge", "sessions.json");
}

/** 取投影单元的值：注册表快照给的是值本身，磁盘投影是 { ver, seq, val } —— 两种形状都认。 */
function unitValue(values, key) {
	if (!values) return undefined;
	const u = values[key];
	if (u === undefined || u === null) return undefined;
	return u.val !== undefined ? u.val : u;
}

function num(v) {
	const n = Number(v);
	return Number.isFinite(n) ? Math.trunc(n) : 0;
}

function str(v) {
	return typeof v === "string" ? v : "";
}

/**
 * 组装快照（**纯函数**，不碰 ctx/磁盘/时钟 → 可单测）。
 * @param {Array<{id:string, live:boolean, values:object, identity?:object}>} sessions
 * @param {string} generatedAt ISO 时间戳（由调用方传入）
 */
export function buildSnapshot(sessions, generatedAt) {
	const out = [];
	for (const s of sessions || []) {
		if (!s) continue;
		const v = s.values || {};
		const stats = unitValue(v, "sessionStats") || {};
		const totals = (unitValue(v, "tokenUsage") || {}).totals || {};
		const pressure = unitValue(v, "contextPressure") || {};
		const meta = unitValue(v, "sessionListMetadata") || {};
		const identity = s.identity || {};
		out.push({
			id: str(s.id),
			live: s.live === true,
			title: str(unitValue(v, "title")),
			cwd: str(identity.cwd),
			createdAt: num(identity.createdAt),
			lastPromptAt: num(meta.lastPromptAt),
			blank: meta.blank === true,
			turns: num(stats.turns),
			steps: num(stats.steps),
			llmMs: num(stats.llmMs),
			toolMs: num(stats.toolMs),
			ttftMs: num(stats.ttftMs),
			decodeMs: num(stats.decodeMs),
			decodeTokens: num(stats.decodeTokens),
			uncachedInputTokens: num(totals.uncachedInputTokens),
			outputTokens: num(totals.outputTokens),
			cacheReadTokens: num(totals.cacheReadTokens),
			cacheWriteTokens: num(totals.cacheWriteTokens),
			contextWindow: num(pressure.contextWindow),
			pressureTokens: num(pressure.pressureTokens),
			surfaceTokens: num(pressure.surfaceTokens),
		});
	}
	return { formatVersion: SNAPSHOT_FORMAT_VERSION, generatedAt: str(generatedAt), sessions: out };
}

/** 原子写（先写临时文件再 rename）——工具箱可能正好在读到一半，不能让它看到半截 JSON。 */
export function writeSnapshot(file, snapshot) {
	fs.mkdirSync(path.dirname(file), { recursive: true });
	const tmp = file + ".tmp-" + process.pid;
	fs.writeFileSync(tmp, JSON.stringify(snapshot), "utf8");
	fs.renameSync(tmp, file);
}

/** 从 ctx 收集会话（防御式：服务名/形状不同就少收集，绝不抛）。 */
export function collectSessions(ctx) {
	const list = [];
	const q = (ctx && (ctx.sessionQuery || ctx.sessions)) || null;
	let raw = [];
	try {
		if (q && typeof q.listSessions === "function") raw = q.listSessions() || [];
		else if (q && typeof q.list === "function") raw = q.list() || [];
	} catch {
		raw = [];
	}
	for (const item of raw) {
		if (!item) continue;
		const session = item.session || item;
		const id = str(item.id || session.id || session.sessionId);
		if (!id) continue;
		let values = {};
		try {
			const snap =
				ctx.sessionProjections && typeof ctx.sessionProjections.snapshot === "function"
					? ctx.sessionProjections.snapshot(session)
					: null;
			values = (snap && snap.values) || {};
		} catch {
			values = {};
		}
		let identity = {};
		try {
			identity = session.meta || session.identity || {};
		} catch {
			identity = {};
		}
		list.push({ id, live: item.live === true, values, identity });
	}
	return list;
}

/**
 * 定时把快照写到磁盘。**放在这里而不是 index.js**：它不 import 任何 dsh 包（只用到 ctx 传进来的对象），
 * 因此可以脱离 dsh 自测（首帧同步写、enabled:false 不写）。index.js 只负责 cordis 声明并重新导出它。
 * 任何失败静默——插件绝不能打断 dsh。
 */
export function apply(ctx, config) {
	const cfg = config || {};
	if (cfg.enabled === false) return;
	const outFile = cfg.outFile && cfg.outFile.trim().length > 0 ? cfg.outFile.trim() : defaultOutFile(process.env);
	const interval = Math.max(1000, Number(cfg.intervalMs) || 3000);
	const tick = () => {
		try {
			const sessions = collectSessions(ctx);
			if (sessions.length === 0) return;
			writeSnapshot(outFile, buildSnapshot(sessions, new Date().toISOString()));
		} catch {
			/* 只读桥：静默降级 */
		}
	};
	const timer = setInterval(tick, interval);
	if (timer && typeof timer.unref === "function") timer.unref(); // 不要因为它而拖住进程退出
	tick();
	try {
		if (ctx && typeof ctx.on === "function") ctx.on("dispose", () => clearInterval(timer));
	} catch {
		/* 没有 dispose 钩子也不影响功能 */
	}
}
