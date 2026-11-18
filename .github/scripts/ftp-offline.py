"""Upload or remove app_offline.htm on the FTP host (IIS serves it instead of the app while present).

Usage: ftp-offline.py upload <local-file> | remove
Env: FTP_SERVER, FTP_USERNAME, FTP_PASSWORD, FTP_DIR
"""
import ftplib
import os
import sys

NAME = "app_offline.htm"

server = os.environ["FTP_SERVER"]
user = os.environ["FTP_USERNAME"]
password = os.environ["FTP_PASSWORD"]
directory = os.environ.get("FTP_DIR", "./").strip()

ftp = ftplib.FTP(server, timeout=60)
ftp.login(user, password)
try:
    if directory not in ("", ".", "./"):
        ftp.cwd(directory)
    if sys.argv[1] == "upload":
        with open(sys.argv[2], "rb") as f:
            ftp.storbinary(f"STOR {NAME}", f)
        print(f"Uploaded {NAME}")
    else:
        try:
            ftp.delete(NAME)
            print(f"Removed {NAME}")
        except ftplib.error_perm as e:
            print(f"Nothing to remove ({e})")
finally:
    ftp.quit()
