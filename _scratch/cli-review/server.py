from http.server import BaseHTTPRequestHandler, HTTPServer

class Handler(BaseHTTPRequestHandler):
    def log_message(self, *args):
        pass

    def do_POST(self):
        n = int(self.headers.get('Content-Length', '0'))
        self.rfile.read(n)
        if self.path == '/Cli/login':
            body = b'0123456789abcdef0123456789abcdef'
            self.send_response(200)
            self.send_header('Content-Length', str(len(body)))
            self.end_headers()
            self.wfile.write(body)
        else:
            self.send_response(200)
            self.end_headers()

    def do_GET(self):
        if self.path == '/Cli':
            table = 'header1\nheader2\nheader3\nrow1\nrow2\n'.encode()
            body = (0).to_bytes(4, 'little', signed=True) + len(table).to_bytes(2, 'little') + table
            self.send_response(200)
            self.send_header('Content-Length', str(len(body)))
            self.end_headers()
            self.wfile.write(body)
        else:
            self.send_response(404)
            self.end_headers()

HTTPServer(('127.0.0.1', 19123), Handler).serve_forever()
