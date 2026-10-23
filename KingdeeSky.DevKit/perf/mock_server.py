#!/usr/bin/env python3
# -*- coding: utf-8 -*-
# Author: rachelli901 <rachelli901@users.noreply.github.com>
# Repository: https://github.com/rachelli901/kingdee-sky-devkit

"""
金蝶云·星空 WebAPI 本地模拟服务（演示用，非金蝶官方）。

在无金蝶云星空运行环境的情况下，用内存实现模拟 KingdeeSky.WebApiClient
调用的 9 个 .kdsvc 端点行为，使客户端可完整跑通：
登录 -> 查询 -> 保存(建单) -> 提交 -> 审核 -> 查看 -> 反审核 -> 删除 -> 下推。

依赖：仅 Python 3 标准库（http.server），无需第三方包。
启动：python mock_server.py [port]   （默认 18080）
"""

import json
import sys
import time
from http.server import BaseHTTPRequestHandler, HTTPServer
from urllib.parse import urlparse

# ---------------------------------------------------------------- 配置
HOST = "127.0.0.1"
PORT = 18080

# 与 KingdeeWebApiClient.cs 中的端点常量保持一致
SVC_AUTH = "Kingdee.BOS.WebApi.ServicesStub.AuthService.ValidateUser.common.kdsvc"
SVC_SAVE = "Kingdee.BOS.WebApi.ServicesStub.DynamicFormService.Save.common.kdsvc"
SVC_SUBMIT = "Kingdee.BOS.WebApi.ServicesStub.DynamicFormService.Submit.common.kdsvc"
SVC_AUDIT = "Kingdee.BOS.WebApi.ServicesStub.DynamicFormService.Audit.common.kdsvc"
SVC_UNAUDIT = "Kingdee.BOS.WebApi.ServicesStub.DynamicFormService.UnAudit.common.kdsvc"
SVC_DELETE = "Kingdee.BOS.WebApi.ServicesStub.DynamicFormService.Delete.common.kdsvc"
SVC_VIEW = "Kingdee.BOS.WebApi.ServicesStub.DynamicFormService.View.common.kdsvc"
SVC_QUERY = (
    "Kingdee.BOS.WebApi.ServicesStub.DynamicFormService.ExecuteBillQuery.common.kdsvc"
)
SVC_PUSH = "Kingdee.BOS.WebApi.ServicesStub.DynamicFormService.Push.common.kdsvc"

# 模拟登录账号（App.config 中 UserName/Password 与此匹配即登录成功）
MOCK_USER = "Administrator"
MOCK_PASSWORD = "demo123"

# 单据状态：A=暂存/草稿  B=已提交  C=已审核
# 模拟库：formId -> { 内码: {model, status, number} }
bills = {}
bill_seq = 100000
bill_no_seq = {}


def next_id():
    global bill_seq
    bill_seq += 1
    return bill_seq


def next_no(form_id):
    global bill_no_seq
    bill_no_seq[form_id] = bill_no_seq.get(form_id, 0) + 1
    return "SKY%s%03d" % (form_id.replace("_", ""), bill_no_seq[form_id])


# ---------------------------------------------------------------- 预置演示数据
def seed_demo_bills():
    """预置 3 张采购订单，让『查询最近 10 张采购订单』开箱即有结果。"""
    demo = [
        {
            "FBillNo": "PO20261018001",
            "FDate": "2026-10-18",
            "FSupplierId.FNumber": "VEN001",
            "FSupplierId.FName": "上海示例供应商",
            "FPurchaseOrgId.FName": "苏州工业园组织",
            "FDocumentStatus": "C",
            "FCreatorName": "Administrator",
        },
        {
            "FBillNo": "PO20261019001",
            "FDate": "2026-10-19",
            "FSupplierId.FNumber": "VEN002",
            "FSupplierId.FName": "北京示例供应商",
            "FPurchaseOrgId.FName": "苏州工业园组织",
            "FDocumentStatus": "B",
            "FCreatorName": "Administrator",
        },
        {
            "FBillNo": "PO20261020001",
            "FDate": "2026-10-20",
            "FSupplierId.FNumber": "VEN003",
            "FSupplierId.FName": "广州示例供应商",
            "FPurchaseOrgId.FName": "苏州工业园组织",
            "FDocumentStatus": "A",
            "FCreatorName": "Administrator",
        },
    ]
    for row in demo:
        bid = next_id()
        bills.setdefault("PUR_PurchaseOrder", {})[bid] = {
            "model": {
                "Id": bid,
                "FBillNo": row["FBillNo"],
                "FDate": row["FDate"],
                "FSupplierId": {
                    "FNumber": row["FSupplierId.FNumber"],
                    "FName": row["FSupplierId.FName"],
                },
                "FPurchaseOrgId": {
                    "FNumber": "100",
                    "FName": row["FPurchaseOrgId.FName"],
                },
                "FDocumentStatus": row["FDocumentStatus"],
                "FCreatorName": row["FCreatorName"],
                "FEntity": [],
            },
            "status": row["FDocumentStatus"],
            "number": row["FBillNo"],
        }


# ---------------------------------------------------------------- 响应构造
def ok_result(message="操作成功", bid=None, number=""):
    entity = {"Id": bid, "Number": number} if bid is not None else {}
    return {
        "Result": {
            "ResponseStatus": {"IsSuccess": True, "Errors": [], "Msg": message},
            "SuccessEntitys": [entity] if bid is not None else [],
        }
    }


def fail_result(message):
    return {
        "Result": {
            "ResponseStatus": {
                "IsSuccess": False,
                "Errors": [{"Message": message}],
                "Msg": message,
            },
            "SuccessEntitys": [],
        }
    }


# ---------------------------------------------------------------- 业务处理
def handle_validate_user(body):
    """登录校验：acctID/username/password/lcid 匹配即成功，返回 LoginResultType==1。"""
    acct = body.get("acctID", "")
    username = body.get("username", "")
    password = body.get("password", "")
    lcid = body.get("lcid", 2052)
    print("[Auth] acctID=%s username=%s lcid=%s" % (acct, username, lcid))
    if username == MOCK_USER and password == MOCK_PASSWORD:
        return {"LoginResultType": 1, "Message": "登录成功"}
    return {"LoginResultType": 0, "Message": "用户名或密码错误"}


def parse_data_json(payload):
    """Save/Submit 等端点的 data 字段是 JSON 字符串，需要二次解析。"""
    raw = payload.get("data", "{}")
    if isinstance(raw, dict):
        return raw
    try:
        return json.loads(raw)
    except Exception:
        return {}


def _enrich_names(node):
    """模拟星空保存时自动带出基础资料名称：值为 {FNumber:...} 且无 FName 时补 FName。"""
    if isinstance(node, dict):
        if "FNumber" in node and "FName" not in node:
            node["FName"] = "演示%s" % node["FNumber"]
        for v in node.values():
            _enrich_names(v)
    elif isinstance(node, list):
        for item in node:
            _enrich_names(item)


def handle_save(body):
    form_id = body.get("formid", "")
    data = parse_data_json(body)
    bid = next_id()
    number = next_no(form_id)
    data["Id"] = bid  # 强制写入内码（客户端传来的 Id=0 仅表示新增）
    data["FBillNo"] = number
    _enrich_names(data)
    bills.setdefault(form_id, {})[bid] = {
        "model": data,
        "status": "A",
        "number": number,
    }
    print("[Save] formid=%s bid=%s number=%s" % (form_id, bid, number))
    return ok_result("保存成功", bid, number)


def _resolve_ids(body):
    """从 Submit/Audit/Delete 的 data 中取 Ids，找不到时回退到 Save 返回的单据。"""
    data = parse_data_json(body)
    ids = data.get("Ids") or []
    if isinstance(ids, list):
        return [int(i) for i in ids if str(i).strip()]
    return []


def _state_action(body, target_status, action_name):
    form_id = body.get("formid", "")
    ids = _resolve_ids(body)
    store = bills.get(form_id, {})
    ok_ids, numbers = [], []
    for bid in ids:
        rec = store.get(bid)
        if rec is None:
            return fail_result("单据 %s 不存在" % bid)
        rec["status"] = target_status
        ok_ids.append(bid)
        numbers.append(rec["number"])
    print("[%s] formid=%s ids=%s" % (action_name, form_id, ids))
    return ok_result(
        action_name + "成功",
        ok_ids[0] if ok_ids else None,
        numbers[0] if numbers else "",
    )


def handle_submit(body):
    return _state_action(body, "B", "提交")


def handle_audit(body):
    return _state_action(body, "C", "审核")


def handle_unaudit(body):
    return _state_action(body, "B", "反审核")


def handle_delete(body):
    form_id = body.get("formid", "")
    ids = _resolve_ids(body)
    store = bills.get(form_id, {})
    for bid in ids:
        rec = store.get(bid)
        if rec is None:
            return fail_result("单据 %s 不存在" % bid)
        if rec["status"] == "C":
            return fail_result("单据 %s 已审核，请先反审核再删除" % bid)
        del store[bid]
    print("[Delete] formid=%s ids=%s" % (form_id, ids))
    return ok_result("删除成功")


def handle_view(body):
    form_id = body.get("formid", "")
    data = parse_data_json(body)
    bid = int(data.get("Id", 0)) if data.get("Id") else 0
    rec = bills.get(form_id, {}).get(bid)
    if rec is None:
        return fail_result("单据 %s 不存在" % bid)
    print("[View] formid=%s bid=%s" % (form_id, bid))
    return {"Result": rec["model"]}


def _get_field(model, path):
    """按 a.b.c 路径取字段值；星空查询常用 基础资料.属性 的嵌套写法。"""
    node = model
    for idx, part in enumerate(path.split(".")):
        if isinstance(node, dict) and part in node:
            node = node[part]
            continue
        # 首个路径段若在顶层存在（如 FDocumentStatus），直接采用
        if idx == 0 and isinstance(model, dict) and part in model:
            node = model[part]
            continue
        return ""
    return node if node is not None else ""


def handle_query(body):
    data = parse_data_json(body)
    form_id = data.get("FormId", "")
    top = int(data.get("TopRowCount") or 0)
    field_keys = data.get("FieldKeys", "")
    fields = [f.strip() for f in field_keys.split(",") if f.strip()]
    store = bills.get(form_id, {})
    rows = []
    for bid in sorted(store.keys(), reverse=True):
        rec = store[bid]
        m = rec["model"]
        row = []
        for f in fields:
            val = _get_field(m, f)
            # FDocumentStatus 不在客户端传入的 model 里，从 mock 维护的状态机补上
            if val == "" and f == "FDocumentStatus" and rec.get("status"):
                val = rec["status"]
            row.append(val)
        rows.append(row)
        if top and len(rows) >= top:
            break
    print(
        "[Query] formid=%s fields=%s top=%s -> %d 行"
        % (form_id, field_keys, top, len(rows))
    )
    return rows


def handle_push(body):
    form_id = body.get("formid", "")
    data = parse_data_json(body)
    target = data.get("TargetFormId", "STK_InStock")
    bid = next_id()
    number = next_no(target)
    bills.setdefault(target, {})[bid] = {
        "model": {
            "Id": bid,
            "FBillNo": number,
            "SourceFormId": form_id,
            "SourceData": data,
        },
        "status": "A",
        "number": number,
    }
    print("[Push] formid=%s -> %s bid=%s number=%s" % (form_id, target, bid, number))
    return ok_result("下推成功", bid, number)


def handle_reset(body):
    """重置演示数据：清空内存库并重新预置 3 张采购订单（供浏览器操作台『重置』按钮）。"""
    bills.clear()
    seed_demo_bills()
    print("[Reset] 演示数据已重置")
    return {"Message": "演示数据已重置"}


# ---------------------------------------------------------------- HTTP 服务
ROUTE_MAP = {
    SVC_AUTH: handle_validate_user,
    SVC_SAVE: handle_save,
    SVC_SUBMIT: handle_submit,
    SVC_AUDIT: handle_audit,
    SVC_UNAUDIT: handle_unaudit,
    SVC_DELETE: handle_delete,
    SVC_VIEW: handle_view,
    SVC_QUERY: handle_query,
    SVC_PUSH: handle_push,
    "__reset__": handle_reset,
}


class MockHandler(BaseHTTPRequestHandler):
    def log_message(self, fmt, *args):
        sys.stderr.write("[http] %s\n" % (fmt % args))

    def do_POST(self):
        parsed = urlparse(self.path)
        length = int(self.headers.get("Content-Length", 0) or 0)
        raw = self.rfile.read(length) if length else b"{}"
        try:
            body = json.loads(raw.decode("utf-8")) if raw.strip() else {}
        except Exception:
            body = {}

        service = parsed.path.replace("/K3Cloud/", "").strip("/")
        handler = ROUTE_MAP.get(service)
        if handler is None:
            self._send(404, {"Message": "未知端点: %s" % self.path})
            return

        try:
            result = handler(body)
            # 模拟金蝶真实行为：ValidateUser 成功后下发会话 Cookie（客户端用 CookieContainer 自动维持）
            # 注意：必须在 send_response(状态行) 之后发送，否则响应头顺序非法，.NET HttpWebRequest 会拒绝
            extra = []
            if service == SVC_AUTH and result.get("LoginResultType") == 1:
                extra.append(("Set-Cookie", "kd_session=demo; Path=/K3Cloud/"))
            self._send(200, result, extra)
        except Exception as ex:
            self._send(500, {"Message": "模拟服务内部错误: %s" % ex})

    def _send(self, code, obj, extra_headers=None):
        payload = json.dumps(obj, ensure_ascii=False).encode("utf-8")
        self.send_response(code)
        # 允许浏览器跨域访问（演示用，生产应按源白名单收紧）
        self.send_header("Access-Control-Allow-Origin", "*")
        self.send_header("Access-Control-Allow-Methods", "POST, OPTIONS")
        self.send_header("Access-Control-Allow-Headers", "Content-Type")
        for name, value in extra_headers or []:
            self.send_header(name, value)
        self.send_header("Content-Type", "application/json; charset=utf-8")
        self.send_header("Content-Length", str(len(payload)))
        self.end_headers()
        self.wfile.write(payload)

    def do_OPTIONS(self):
        # 浏览器对跨域 POST(Content-Type: application/json) 会先发预检
        self.send_response(204)
        self.send_header("Access-Control-Allow-Origin", "*")
        self.send_header("Access-Control-Allow-Methods", "POST, OPTIONS")
        self.send_header("Access-Control-Allow-Headers", "Content-Type")
        self.send_header("Access-Control-Max-Age", "86400")
        self.end_headers()


def main():
    global PORT
    if len(sys.argv) > 1:
        PORT = int(sys.argv[1])
    seed_demo_bills()
    server = HTTPServer((HOST, PORT), MockHandler)
    print("金蝶云·星空 WebAPI 本地模拟服务已启动：http://%s:%d/K3Cloud/" % (HOST, PORT))
    print(
        "已覆盖端点：ValidateUser / Save / Submit / Audit / UnAudit / Delete / View / ExecuteBillQuery / Push"
    )
    print("模拟账号：%s / %s   （Ctrl+C 停止）" % (MOCK_USER, MOCK_PASSWORD))
    try:
        server.serve_forever()
    except KeyboardInterrupt:
        print("\n模拟服务已停止。")


if __name__ == "__main__":
    main()
