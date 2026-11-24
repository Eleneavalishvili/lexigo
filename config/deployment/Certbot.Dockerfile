FROM certbot/certbot:v5.10.0@sha256:c5698da859429ccee0fcc299a8000368198222d1f9e789ab6e388b491e8ce06d
COPY config/deployment/certbot-requirements.txt /tmp/lexigo-certbot-requirements.txt
RUN python -m pip install --no-cache-dir --upgrade -r /tmp/lexigo-certbot-requirements.txt \
    && python -m pip check \
    && python -m pip uninstall --yes pip \
    && python -c "import shutil, sysconfig; shutil.rmtree(sysconfig.get_path('stdlib') + '/ensurepip')"
