#!/usr/bin/python3
# -*- coding: utf-8 -*-
"""
    Name: ifttt_proxy.py
    Arguments:
        -h, --help: print usage on stdout
        -V, --version: print script version on stdout
        -t, --target: the mac address of the target machine on the lan (format 00:00:00:00:00:00)
        -p, --port: listening port for the http server.
    Description:
        This script is a proxy between IFTTT cloud service and a lan PC. It gets the given lan PC
        mac address from command line, launch a simmple http server listening on the given port
	and answer to get request by waking up the lan pc.
    Dependencies:
        flask: rest api implementation
        wakeonlan: send magic packet
        sdnotify: systemd notifier
"""

import sys
import os
import argparse
import logging
from flask import Flask, request, Response, abort
from wakeonlan import send_magic_packet
from sdnotify import SystemdNotifier


# Script information
__version_info__ = ('0', '0', '2')
__version__ = '.'.join(__version_info__)
__date__ = "2020-05-07"
__author__ = "Denis Lambolez"
__contact__ = "denis.lambolez@gmail.com"
__license__ = "MIT"

# Global constants
LOG_DIR = "/var/log"
# LOG_DIR = os.path.dirname(os.path.realpath(__file__))
LOG_LEVEL = logging.INFO
SCRIPT_NAME = "ifttt_proxy"


class FlaskApi:
    """The Api class
    Configure and run the api
    """
    def __init__(self, name, port, target, logger):
        """Api initialization
        name: api name
        port: the listening port for the server
        target: mac address of the target
        logger: api logger
        """
        self._port = port
        self._target = target
        self._logger = logger
        self._api = Flask(name)
        self._api.add_url_rule("/" + SCRIPT_NAME + "/api/v1.0/wakeup", "get_wakeup",
                               self.EndpointAction(self._get_wakeup, False))
        self._api.add_url_rule("/" + SCRIPT_NAME + "/api/v1.0/shutdown", "get_shutdown",
                               self.EndpointAction(self._shutdown, False))

    def run(self):
        """Start the application server"""
        self._api.run(host="0.0.0.0", port=self._port)

    def _get_wakeup(self):
        """Wakeup api action"""
        self._logger.info("Request on %s from %s", request.path, request.remote_addr)
        # Wake up the target
        self._logger.info("Send magic packet to target with mac address: %s", self._target)
        send_magic_packet(self._target)
        # return OK
        return "Waking up target..."

    def _shutdown(self):
        """Shutdown api action"""
        self._logger.info("Request on %s from %s", request.path, request.remote_addr)
        func = request.environ.get("werkzeug.server.shutdown")
        if func is None:
            raise RuntimeError("Not running with the Werkzeug Server")
        self._logger.info("Gracefully shutting down the server...")
        func()
        return "Server shutting down..."

    class EndpointAction:
        """Define an action attached to an end point"""

        def __init__(self, action, no_content):
            """Initialize the end point
            action: action handler
            no_content: true if action return no content (204 vs 200)
            """
            self._action = action
            self._no_content = no_content

        def __call__(self):
            """Execute the action and return the response"""
            answer = self._action()
            if bool(self._no_content):
                # return ok with no content
                return ("", 204)
            # return ok with the answer
            return Response(answer, status=200, headers={})


def main():
    """Script main function.
    Does logging initialization and command line arguments parsing.
    """

    # Initialize script logger
    log_file = os.path.join(LOG_DIR, SCRIPT_NAME + ".log")

    formatter = logging.Formatter("%(asctime)s - %(name)s - %(levelname)s - %(message)s")
    logger_file_handler = logging.FileHandler(log_file)
    logger_file_handler.setLevel(logging.DEBUG)
    logger_file_handler.setFormatter(formatter)
    logger = logging.getLogger(SCRIPT_NAME)
    logger.setLevel(LOG_LEVEL)
    logger.addHandler(logger_file_handler)
    logger.info("====== %s initialization ======", SCRIPT_NAME)
    logger.debug("sys.path: %s", str(sys.path))

    # Parse command line arguments
    parser = argparse.ArgumentParser(description="ifttt proxy for lan pc")
    parser.add_argument("-t", "--target", required=True,
                        help="mac address of the target machine on the lan (00:00:00:00:00:00")
    parser.add_argument("-p", "--port", default=33000, type=int,
                        help="listening port for the http server (default: 33000)")
    parser.add_argument("-V", "--version", action="version", version='%(prog)s ' + __version__
                        + ' - ' + __date__, help="show current version of the script")
    args = parser.parse_args()
    logger.info("Starting with port: %i and target: %s", args.port, args.target)

    # Initialize the api
    api = FlaskApi(__name__, args.port, args.target, logger)
    # Notify systemd that we are ready
    n = SystemdNotifier()
    n.notify("READY=1")
    # Run the api
    api.run()


# Module entry point with remote debug
if __name__ == "__main__":
    main()
