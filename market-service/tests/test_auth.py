def test_health_no_auth(client):
    response = client.get("/health")
    assert response.status_code == 200

def test_ticker_requires_auth(client):
    response = client.get("/ticker/RELIANCE")
    assert response.status_code == 401

def test_ticker_wrong_auth(client):
    response = client.get("/ticker/RELIANCE", headers={"X-Internal-Key": "wrong"})
    assert response.status_code == 401

def test_ticker_valid_auth(client, auth_headers, mock_yf_ticker):
    response = client.get("/ticker/RELIANCE", headers=auth_headers)
    assert response.status_code == 200
