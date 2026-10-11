#!/usr/bin/env python3
"""Serve CHASE WebGL with correct gzip headers and *no stale build caching*.

From repository root:
    python3 Tools/serve_webgl.py --build Builds/ChaseWebGL --port 8765
    python3 Tools/serve_webgl.py --build Builds/ChaseAnimationGymWebGL --port 8766

The browser must still reload the page to replace an already-running Unity
instance. A rebuild cannot update a Unity instance that's already loaded.
"""
import argparse
from functools import partial
from http.server import HTTPServer, SimpleHTTPRequestHandler
from pathlib import Path


class BuildHandler(SimpleHTTPRequestHandler):
    def guess_type(self, path):
        if path.endswith('.js.gz'):
            return 'application/javascript'
        if path.endswith('.wasm.gz'):
            return 'application/wasm'
        if path.endswith('.data.gz'):
            return 'application/octet-stream'
        return super().guess_type(path)

    def end_headers(self):
        if self.path.split('?', 1)[0].endswith('.gz'):
            self.send_header('Content-Encoding', 'gzip')
        self.send_header('Cache-Control', 'no-store, max-age=0')
        self.send_header('Pragma', 'no-cache')
        self.send_header('Cross-Origin-Opener-Policy', 'same-origin')
        self.send_header('Cross-Origin-Embedder-Policy', 'require-corp')
        super().end_headers()


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--build', default='Builds/ChaseWebGL')
    parser.add_argument('--port', type=int, default=8765)
    args = parser.parse_args()
    directory = Path(args.build).resolve(strict=True)
    if not (directory / 'index.html').is_file():
        parser.error('WebGL build directory must contain index.html: ' + str(directory))
    print(f'CHASE WebGL: http://127.0.0.1:{args.port}/ -> {directory}', flush=True)
    HTTPServer(('127.0.0.1', args.port), partial(BuildHandler, directory=str(directory))).serve_forever()


if __name__ == '__main__':
    main()
