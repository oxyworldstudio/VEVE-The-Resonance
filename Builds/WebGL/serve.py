import http.server
import socketserver
import os
import mimetypes

PORT = 8080
DIRECTORY = os.path.dirname(os.path.abspath(__file__))

class GzipHTTPRequestHandler(http.server.SimpleHTTPRequestHandler):
    def guess_type(self, path):
        if path.endswith('.gz'):
            if path.endswith('.js.gz'):
                return 'application/javascript'
            if path.endswith('.wasm.gz'):
                return 'application/wasm'
            if path.endswith('.data.gz'):
                return 'application/octet-stream'
            return 'application/octet-stream'
        return super().guess_type(path)

    def end_headers(self):
        if self.path.endswith('.gz'):
            self.send_header('Content-Encoding', 'gzip')
        super().end_headers()

    def log_message(self, format, *args):
        print(f"[SERVER] {format % args}")

os.chdir(DIRECTORY)
with socketserver.TCPServer(("", PORT), GzipHTTPRequestHandler) as httpd:
    print(f"Serving at http://localhost:{PORT}")
    httpd.serve_forever()
