using System;
using System.Collections.Generic;
using FEncrypt;
using Newtonsoft.Json;
using UnityEngine;

namespace FTools.Network
{
    public static class FRequestPayload
    {
        private const string AES_KEY = "2m+yb/Ln37qwpMPwfueZGj4Ms23v8+wWi/uBH8F/Cqk=";

        private const string RSA_PRIVATE_KEY = @"
-----BEGIN PRIVATE KEY-----
MIIEvQIBADANBgkqhkiG9w0BAQEFAASCBKcwggSjAgEAAoIBAQDBv5JbyAptvdqv
WS0hvm/L5FH6x1zhFEB/4dX9KGKhKli94R4hhGQpWf7IijYqBf3mn9c21PS05wQI
M3Tr+ENEpBUI8nc3HDJ2k7EJ7mytwvnQQ6uKgQx5dZFBTB8oFf8/z2mn5hfInpl2
MHuGqVx5wTqBgCFgT6SmOLsVLPNgA9YabIigswsnUHN39x6X3mbGF+e4kk/UWOdr
RX5XpnzUdBznV8QUTVLNJ6eTpdZv3X/f7NmmIdPbZhm6BA0U1sqdPe8Tn1xh2u2b
qZBAkTHsF1SJOTy+gVNW0bo6qXPCJSJ9IwjU3p/S4/YCQGGYXCp6nW03pgBf52gW
Yt+jiBONAgMBAAECggEABJq+ajL7WHdSGqe2ZF5nvsM61YhmYyz9iWx/DuimS2k7
uat/cVF5pefvH6n1NpN04MW8sh9mf2QcGuvKJA9+3cM+grtsbOFdK6c/5loFuC1x
jWsGSSDfAIX0QUaDWQsIyWXu/1//AbQPNplZXi5pNbWLY+d9I5JQ+ehQPeUXFZmH
Jmi6Fd4xIz9mJeEMQrR1Tnpk1te7R9K+ywVyI49FkkT+PVYswzXNwZjQbAja//iO
/H6rgFxhKZ/3hyCXvE1MSnmB4dww70rZv0k16JeNr7rqt5bLdtx+NLWQq3k3l2S4
FEVe/HuR95eL5dvcxcKEV77mzgslkStclmuAQ7EBQQKBgQDljLODAtbtHZuFglli
pIuDEe4XUMFbFxQK5D/CAJ0G/FKIfgxj3HL/k+UsmZ0zIG9MjxcpSm2d6n3nIjlx
oWC6C+L+YnQQ5X5ftIs+OaCr2eKStww8C+3Fn52aN2RmZO6lVVK2yAuDzcr7ZZkT
KYxVBQc9sljK8XOlqAhXLRJ4bQKBgQDYEswW3AbaABXXJidzLU8bby3J9NSiTQC2
btJDSJZh8v6Rew9dsHyPUNWqv/eJx6eSOnSMmoLlpcU+qIl1auiczzCjtVkvyva5
G83+4++S7V8CAUPCNnyyFub5oIEeB3HpZGQLEWro4Bzs5yaWE4qJZZpeX8B+Rprr
JcWBR/tToQKBgQCj1mDpyUUNyGwQRpD1zuukiU+eZI0dLsOnYnH/TeDF86IUuaKu
Q9HwiOaxhpKkwymtQxQM7IqAg7WJGuxVJhGbuJAzcKHWzXX9aSdZKlh7T/BfNzcM
RJdYziv2vkJysueCKfv3aaCOWnveIw22l5kJtaUNbBXkA6G/QsdxMUJVoQKBgF9e
PJJCXwvNSdaCUps1flr/gVhIJanVDuF2nHtyCF4HIHEZvbh+cxvWc9g/joRTiRSz
lEmS0P+HYT28qZ8Xpr+/expmqoy7qpYdPZipBk71Uh/SaxPHdNYt4ZiATTzx0EdR
Ng7kiylp8Zpmk9NdoKHwJGhPHukdRLdRHpFnMfjBAoGAea9LUlRw9dJrNTLsG/2s
a5wt72s/dLUCXNPpwIc7WbFLDRsBkZcUbUBB1AxyvXxnG0QwKD0YNb6oSVm5I7Oz
DTDMPZssWyb6Xrmf2GZAidxIWlTMzL0hAiEgWzJ0fNOQZsyzOXd+7iV0dZSZv+EA
nWX2JC9lpG6p6N2q2M+0AZo=
-----END PRIVATE KEY-----";

        public static string MakeRequestPayload(string jsonStr)
        {
            var encryptedData = FAes.Encrypt(jsonStr, AES_KEY);
            
            var timestamp = DateTimeOffset.Now.ToUnixTimeMilliseconds();
            var nonce = Guid.NewGuid().ToString().Replace("-", "");
            var signature = FRsa.Sign($"{encryptedData}:{timestamp}:{nonce}", RSA_PRIVATE_KEY);
            var data = new Dictionary<string, object>
            {
                { "data", encryptedData },
                { "timestamp", timestamp },
                { "nonce", nonce },
                { "sign", signature }
            };
            return JsonConvert.SerializeObject(data);
        }
    }
}