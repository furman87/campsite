# Lakeside Campground

A mobile-first campground management site written in **.NET 10 MVC**, backed by **PostgreSQL**. Every application query and command uses **Dapper**. The customer experience includes campsite availability and booking, activities, visit information, restaurant menu, and secure hosted card/PayPal checkout. The administrator area handles reservations, customers, payment follow-up, activities, menu items, and the site’s color palette.

## What is included

- Campsite availability checks that prevent overlapping active reservations
- Stripe Checkout for cards and PayPal hosted checkout, with server-side completion verification
- Cookie-protected administrator login with PBKDF2-SHA512 password hashes
- Auto-created schema and starter campground data at first launch
- Responsive green, brown, beige, and ember-orange design; editable from **Admin → Brand colors**
- Generated abstract orange campfire logo at `wwwroot/campfire-logo.png`

## Deploy on Ubuntu 24.04 with Docker and Nginx

Docker and Nginx are assumed to be installed.

1. Clone the project from GitHub and enter the deployment directory. Replace the example repository URL with your GitHub repository:

   ```bash
   sudo git clone https://github.com/your-org/your-repository.git /opt/campsite
   cd /opt/campsite
   cp .env.example .env
   chmod 600 .env
   nano .env
   ```

   Set long, unique values for `POSTGRES_PASSWORD` and `ADMIN_PASSWORD` before the first start. Configure the payment values when you are ready to accept payments. Leave the Stripe/PayPal values blank only for a visual/local preview; booking checkout will then tell you that the selected provider needs configuration.

2. Start the website and database:

   ```bash
   docker compose up -d --build
   docker compose logs -f app
   ```

   The app initializes its tables and starter data automatically. It is published only on `127.0.0.1:8081`, so public access goes through Nginx.

3. Configure Nginx. Edit `deploy/nginx/lakeside-campground.conf` and replace both `example.com` entries with the real domain, then install it:

   ```bash
   sudo cp deploy/nginx/lakeside-campground.conf /etc/nginx/sites-available/lakeside-campground
   sudo ln -s /etc/nginx/sites-available/lakeside-campground /etc/nginx/sites-enabled/lakeside-campground
   sudo nginx -t
   sudo systemctl reload nginx
   ```

4. Add HTTPS after DNS points at the server, for example with Certbot:

   ```bash
   sudo certbot --nginx -d example.com -d www.example.com
   ```

## Payments

For cards, create a Stripe secret key and set `STRIPE_SECRET_KEY=sk_live_...` (use `sk_test_...` while testing). Stripe Checkout collects card data directly; this app never handles it. The success return checks the Checkout Session with Stripe before recording payment as paid.

For PayPal, set `PAYPAL_CLIENT_ID`, `PAYPAL_CLIENT_SECRET`, and use the sandbox base URL for testing. Change `PAYPAL_BASE_URL` to `https://api-m.paypal.com` for live operation. The app captures and verifies the returned order server-side.

Use a real HTTPS domain for live payments. Configure your Stripe and PayPal dashboard return/webhook policies according to your organization’s security requirements.

## Administration

Open `/Account/Login` and use the `ADMIN_USERNAME` / `ADMIN_PASSWORD` set in `.env`. The initial account is created only if no administrator exists, so choose the production password before the first startup. The administrator dashboard supports reservation status changes, activity and menu management, and color customisation.

## Operations

Update after source changes with `docker compose up -d --build`. Database data is retained in the named `campground-postgres` volume. Back it up regularly, for example:

```bash
docker compose exec -T db pg_dump -U "$POSTGRES_USER" "$POSTGRES_DB" > lakeside-backup.sql
```

For development outside Docker, update `ConnectionStrings:Campground` in `appsettings.Development.json` (or user secrets) and run `dotnet run`.
