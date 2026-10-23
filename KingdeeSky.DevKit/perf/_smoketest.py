import urllib.request, urllib.error, json, http.client

BASE = "http://127.0.0.1:18080/K3Cloud/"


def post(svc, body, origin=None):
    req = urllib.request.Request(BASE + svc, data=json.dumps(body).encode(),
                                 headers={"Content-Type": "application/json"})
    if origin:
        req.add_header("Origin", origin)
    try:
        r = urllib.request.urlopen(req)
        return r.status, r.headers.get("Access-Control-Allow-Origin"), json.loads(r.read().decode())
    except urllib.error.HTTPError as e:
        return e.code, e.headers.get("Access-Control-Allow-Origin"), e.read().decode()[:200]
    except Exception as e:  # noqa
        return "ERR", None, str(e)


conn = http.client.HTTPConnection("127.0.0.1", 18080)
conn.request("OPTIONS", BASE + "Kingdee.BOS.WebApi.ServicesStub.AuthService.ValidateUser.common.kdsvc",
             headers={"Origin": "http://127.0.0.1:8080"})
resp = conn.getresponse()
print("OPTIONS status:", resp.status, "CORS:", resp.getheader("Access-Control-Allow-Origin"))
conn.close()

print("LOGIN ok :", post("Kingdee.BOS.WebApi.ServicesStub.AuthService.ValidateUser.common.kdsvc",
                         {"acctID": "DEMO", "username": "Administrator", "password": "demo123", "lcid": 2052},
                         "http://127.0.0.1:8080"))
print("LOGIN bad:", post("Kingdee.BOS.WebApi.ServicesStub.AuthService.ValidateUser.common.kdsvc",
                         {"acctID": "DEMO", "username": "x", "password": "y", "lcid": 2052}))
print("SAVE     :", post("Kingdee.BOS.WebApi.ServicesStub.DynamicFormService.Save.common.kdsvc",
                         {"formid": "PUR_PurchaseOrder",
                          "data": json.dumps({"FSupplierId": {"FNumber": "VEN001"}, "FDate": "2026-10-23",
                                              "FEntity": [{"FMaterialId": {"FNumber": "M001"}, "FQty": 10, "FPrice": 88.5}]})}))
print("QUERY    :", post("Kingdee.BOS.WebApi.ServicesStub.DynamicFormService.ExecuteBillQuery.common.kdsvc",
                         {"formid": "PUR_PurchaseOrder",
                          "data": json.dumps({"FormId": "PUR_PurchaseOrder",
                                              "FieldKeys": "Id,FBillNo,FDocumentStatus,FSupplierId.FName",
                                              "TopRowCount": 10})}))
print("RESET    :", post("__reset__", {}))
