/**
 * 零依赖自测（只用 node 内置 assert / fs / os / path）。
 * 运行：node test/snapshot.test.js   （在 plugin/dsh-shio-bridge 目录下）
 * 覆盖：纯函数 buildSnapshot 的字段映射与两种投影形状、防御式 collectSessions、
 *       原子写、defaultOutFile 的 DSH_HOME 语义、apply 的首帧写入与 enabled:false。
 * 注意：这里**不验证真实 dsh 的 ctx 形状**（那需要跑一次 dsh）——本测试只保证我们自己的逻辑与契约。
 */
import assert from "node:assert/strict";
import fs from "node:fs";
import os from "node:os";
import path from "node:path";
import { buildSnapshot, collectSessions, defaultOutFile, writeSnapshot, apply, SNAPSHOT_FORMAT_VERSION } from "../snapshot.js";

let pass = 0;
function check(name, fn) {
	try {
		fn();
		pass++;
		console.log("  [PASS] " + name);
	} catch (e) {
		console.log("  [FAIL] " + name + " -> " + e.message);
		process.exitCode = 1;
	}
}

console.log("== dsh-shio-bridge 自测（零依赖）==");

// ---- buildSnapshot：字段映射 ----
const bare = {
	id: "s1",
	live: true,
	identity: { cwd: "D:\\work", createdAt: 1788517824758 },
	values: {
		sessionStats: { turns: 2, steps: 9, llmMs: 1000, toolMs: 500, ttftMs: 300, decodeMs: 2000, decodeTokens: 400 },
		tokenUsage: { totals: { uncachedInputTokens: 100, outputTokens: 50, cacheReadTokens: 900, cacheWriteTokens: 10 } },
		contextPressure: { surfaceTokens: 500, contextWindow: 1000, pressureTokens: 250 },
		sessionListMetadata: { blank: false, lastPromptAt: 1788517999999 },
		title: "hello",
	},
};
const snap = buildSnapshot([bare], "2026-09-28T00:00:00Z");
check("格式版本为 2", () => assert.equal(snap.formatVersion, SNAPSHOT_FORMAT_VERSION));
check("generatedAt 由调用方传入（纯函数不读时钟）", () => assert.equal(snap.generatedAt, "2026-09-28T00:00:00Z"));
check("会话数与 live 标记", () => assert.equal(snap.sessions.length, 1) && assert.equal(snap.sessions[0].live, true));
check("token 四个字段", () => {
	const s = snap.sessions[0];
	assert.equal(s.uncachedInputTokens, 100);
	assert.equal(s.outputTokens, 50);
	assert.equal(s.cacheReadTokens, 900);
	assert.equal(s.cacheWriteTokens, 10);
});
check("会话统计与上下文压力", () => {
	const s = snap.sessions[0];
	assert.equal(s.turns, 2);
	assert.equal(s.steps, 9);
	assert.equal(s.ttftMs, 300);
	assert.equal(s.decodeMs, 2000);
	assert.equal(s.decodeTokens, 400);
	assert.equal(s.contextWindow, 1000);
	assert.equal(s.pressureTokens, 250);
});
check("identity / title / 时间戳", () => {
	const s = snap.sessions[0];
	assert.equal(s.cwd, "D:\\work");
	assert.equal(s.createdAt, 1788517824758);
	assert.equal(s.lastPromptAt, 1788517999999);
	assert.equal(s.title, "hello");
	assert.equal(s.blank, false);
});

// ---- buildSnapshot：磁盘投影形状（{ver,seq,val}）也要认 ----
const diskish = { id: "s2", live: false, values: { sessionStats: { ver: 1, seq: 2, val: { turns: 5 } }, tokenUsage: { val: { totals: { outputTokens: 7 } } } } };
check("兼容磁盘投影的 {ver,seq,val} 形状", () => {
	const s = buildSnapshot([diskish], "t").sessions[0];
	assert.equal(s.turns, 5);
	assert.equal(s.outputTokens, 7);
});
check("缺字段/空值 → 0 与空串（不抛）", () => {
	const s = buildSnapshot([{ id: "s3" }], "t").sessions[0];
	assert.equal(s.turns, 0);
	assert.equal(s.live, false);
	assert.equal(s.title, "");
	assert.equal(s.createdAt, 0);
});
check("null/空列表 → 空 sessions", () => {
	assert.equal(buildSnapshot(null, "t").sessions.length, 0);
	assert.equal(buildSnapshot([null, undefined], "t").sessions.length, 0);
});

// ---- defaultOutFile ----
check("defaultOutFile 跟随 DSH_HOME", () => {
	const p = defaultOutFile({ DSH_HOME: path.join("X:", "iso", "home") });
	assert.equal(p, path.join("X:", "iso", "home", "shio-bridge", "sessions.json"));
});
check("defaultOutFile：DSH_HOME 为空 → 退回主目录 .dsh", () => {
	const p = defaultOutFile({ DSH_HOME: "   " });
	assert.ok(p.endsWith(path.join(".dsh", "shio-bridge", "sessions.json")));
});

// ---- 原子写 ----
check("writeSnapshot 写文件且不留 .tmp", () => {
	const dir = fs.mkdtempSync(path.join(os.tmpdir(), "bridge-test-"));
	const file = path.join(dir, "sub", "sessions.json");
	writeSnapshot(file, snap);
	const back = JSON.parse(fs.readFileSync(file, "utf8"));
	assert.equal(back.formatVersion, SNAPSHOT_FORMAT_VERSION);
	const leftovers = fs.readdirSync(path.dirname(file)).filter((n) => n.includes(".tmp-"));
	assert.equal(leftovers.length, 0);
	fs.rmSync(dir, { recursive: true, force: true });
});

// ---- collectSessions：防御式 ----
check("collectSessions：listSessions + snapshot 正常路径", () => {
	const ctx = {
		sessionQuery: { listSessions: () => [{ id: "a", live: true, session: { id: "a", meta: { cwd: "C:\\w" } } }] },
		sessionProjections: { snapshot: () => ({ values: { sessionStats: { turns: 3 } } }) },
	};
	const list = collectSessions(ctx);
	assert.equal(list.length, 1);
	assert.equal(list[0].id, "a");
	assert.equal(list[0].live, true);
	assert.equal(list[0].identity.cwd, "C:\\w");
	assert.equal(list[0].values.sessionStats.turns, 3);
});
check("collectSessions：服务缺失/抛异常 → 空数组（不抛）", () => {
	assert.equal(collectSessions({}).length, 0);
	assert.equal(collectSessions({ sessionQuery: { listSessions: () => { throw new Error("boom"); } } }).length, 0);
	assert.equal(collectSessions({ sessionQuery: { listSessions: () => [{ id: "" }] } }).length, 0);
});

// ---- apply ----
check("apply：首帧同步写快照；enabled:false 不写", () => {
	const dir = fs.mkdtempSync(path.join(os.tmpdir(), "bridge-apply-"));
	const file = path.join(dir, "sessions.json");
	const ctx = {
		sessionQuery: { listSessions: () => [{ id: "a", live: false }] },
		sessionProjections: { snapshot: () => ({ values: { sessionStats: { turns: 1 } } }) },
		on: () => {},
	};
	apply(ctx, { outFile: file, intervalMs: 100000 });
	assert.ok(fs.existsSync(file), "首帧应已写出快照");
	assert.equal(JSON.parse(fs.readFileSync(file, "utf8")).sessions.length, 1);
	const off = path.join(dir, "off.json");
	apply(ctx, { outFile: off, enabled: false, intervalMs: 100000 });
	assert.equal(fs.existsSync(off), false, "enabled:false 不应写文件");
	fs.rmSync(dir, { recursive: true, force: true });
});

console.log("\n== " + pass + " passed ==");
