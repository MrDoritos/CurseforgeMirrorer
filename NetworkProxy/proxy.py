#!/usr/bin/python
import cloudscraper
import socketserver
import http.server
import urllib

PORT=9097

scraper = cloudscraper.create_scraper(
    browser={
        'browser': 'firefox',
        'platform': 'linux',
        'desktop': True,
	'mobile': False
    },
    interpreter='nodejs',
    captcha={
      'provider': 'anticaptcha',
      'api_key': 'd761540422ad3d64b642f6df86182a1c',
      'no_proxy': True
    },
    #debug=True
)

class MyProxy(http.server.SimpleHTTPRequestHandler):
    def do_GET(self):
        url=self.path
        self.send_response(200)
        self.end_headers()
        print(url)
        url = url.replace('http://', 'https://')
        print(url)
        content = scraper.get(url).content
        self.wfile.write(content)

httpd = socketserver.ForkingTCPServer(('', PORT), MyProxy)
httpd.serve_forever()
