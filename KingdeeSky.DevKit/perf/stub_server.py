#!/usr/bin/env python3
# -*- coding: utf-8 -*-
# Author: rachelli901 <rachelli901@users.noreply.github.com>
# Repository: https://github.com/rachelli901/kingdee-sky-devkit

"""Minimal Kingdee K/3 Cloud WebAPI stub for client-side performance smoke tests.

Implements only the endpoints used by PerfProbe with fixed in-memory responses:
  - AuthService.ValidateUser            -> {"LoginResultType":1} + Set-Cookie
  - DynamicFormService.ExecuteBillQuery -> fixed 2-D array (3 rows)
  - anything else                       -> generic {"Result":{"ResponseStatus":...}}
Stdlib only. Usage:
  python stub_server.py [-p 18081] [-d 0]
"""

import argparse
import json
import time
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer

VALIDATE_USER = "Kingdee.BOS.WebApi.ServicesStub.AuthService.ValidateUser.common.kdsvc"
EXECUTE_BILL_QUERY = (
    "Kingdee.BOS.WebApi.ServicesStub.DynamicFormService.ExecuteBillQuery.common.kdsvc"
)

GENERIC_OK = {
    "Result": {
        "ResponseStatus": {"IsSuccess": True, "Errors": [], "Msg": "perf-ok"},
        "SuccessEntitys": [],
    }
}


def make_handler(delay_ms):
    class Handler(BaseHTTPRequestHandler):
        request_count = 0

        def log_message(self, fmt, *args):  # silence default stderr logging
            pass

        def do_POST(self):
            Handler.request_count += 1
            length = int(self.headers.get("Content-Length") or 0)
            if length > 0:
                self.rfile.read(length)
            if delay_ms and delay_ms > 0:
                time.sleep(delay_ms / 1000.0)
            if VALIDATE_USER in self.path:
                body = json.dumps({"LoginResultType": 1}).encode("utf-8")
                self.send_response(200)
                self.send_header("Content-Type", "application/json; charset=utf-8")
                self.send_header("Set-Cookie", "kdsid=perf123; Path=/")
            elif EXECUTE_BILL_QUERY in self.path:
                rows = [
                    ["PERF001", "2026-10-20", "C"],
                    ["PERF002", "2026-10-21", "B"],
                    ["PERF003", "2026-10-22", "A"],
                ]
                body = json.dumps(rows).encode("utf-8")
                self.send_response(200)
                self.send_header("Content-Type", "application/json; charset=utf-8")
            else:
                body = json.dumps(GENERIC_OK).encode("utf-8")
                self.send_response(200)
                self.send_header("Content-Type", "application/json; charset=utf-8")
            self.send_header("Content-Length", str(len(body)))
            self.end_headers()
            self.wfile.write(body)

    return Handler


def main():
    parser = argparse.ArgumentParser(
        description="Minimal Kingdee WebAPI stub (perf smoke)"
    )
    parser.add_argument("-p", "--port", type=int, default=18081, help="listen port")
    parser.add_argument(
        "-d",
        "--delay",
        type=int,
        default=0,
        help="artificial per-response delay in milliseconds",
    )
    args = parser.parse_args()
    server = ThreadingHTTPServer(("127.0.0.1", args.port), make_handler(args.delay))
    print(
        "PERF-STUB listening on 127.0.0.1:%d delay=%dms" % (args.port, args.delay),
        flush=True,
    )
    try:
        server.serve_forever()
    except KeyboardInterrupt:
        pass
    finally:
        server.server_close()


if __name__ == "__main__":
    main()
