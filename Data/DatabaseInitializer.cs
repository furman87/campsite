using Campsite.Services;
using Dapper;
using Npgsql;

namespace Campsite.Data;
public sealed class DatabaseInitializer(NpgsqlDataSource dataSource, IConfiguration configuration, PasswordService password)
{
    public async Task InitializeAsync()
    {
        const string schema = """
        CREATE TABLE IF NOT EXISTS campsites (id serial PRIMARY KEY,name varchar(80) NOT NULL,site_type varchar(40) NOT NULL,description text NOT NULL,nightly_rate numeric(10,2) NOT NULL,max_guests int NOT NULL,has_electric boolean NOT NULL DEFAULT false,is_active boolean NOT NULL DEFAULT true);
        CREATE TABLE IF NOT EXISTS customers (id serial PRIMARY KEY,name varchar(90) NOT NULL,email varchar(180) NOT NULL UNIQUE,phone varchar(40) NOT NULL DEFAULT '',created_at timestamptz NOT NULL DEFAULT now());
        CREATE TABLE IF NOT EXISTS reservations (id serial PRIMARY KEY,campsite_id int NOT NULL REFERENCES campsites(id),customer_id int NOT NULL REFERENCES customers(id),check_in date NOT NULL,check_out date NOT NULL,guests int NOT NULL,total numeric(10,2) NOT NULL,status varchar(20) NOT NULL,payment_status varchar(20) NOT NULL,payment_method varchar(20) NOT NULL,payment_reference varchar(160),created_at timestamptz NOT NULL DEFAULT now(),CHECK(check_out > check_in));
        CREATE INDEX IF NOT EXISTS ix_reservations_availability ON reservations(campsite_id,check_in,check_out);
        CREATE TABLE IF NOT EXISTS activities (id serial PRIMARY KEY,title varchar(120) NOT NULL,description text NOT NULL,starts_at timestamptz NOT NULL,location varchar(100) NOT NULL);
        CREATE TABLE IF NOT EXISTS menu_items (id serial PRIMARY KEY,category varchar(60) NOT NULL,name varchar(100) NOT NULL,description text NOT NULL,price numeric(10,2) NOT NULL,is_available boolean NOT NULL DEFAULT true);
        CREATE TABLE IF NOT EXISTS settings (key varchar(80) PRIMARY KEY,value text NOT NULL);
        CREATE TABLE IF NOT EXISTS app_users (id serial PRIMARY KEY,username varchar(80) NOT NULL UNIQUE,password_hash text NOT NULL,role varchar(30) NOT NULL DEFAULT 'Administrator');
        """;
        await using var db = await dataSource.OpenConnectionAsync(); await db.ExecuteAsync(schema);
        if (await db.ExecuteScalarAsync<int>("SELECT count(*) FROM campsites") == 0)
            await db.ExecuteAsync("INSERT INTO campsites(name,site_type,description,nightly_rate,max_guests,has_electric) VALUES ('Lakeview RV 01','RV Site','Level back-in site with water, 30-amp electric and a sunset lake view.',62,6,true),('Pine Loop 07','RV Site','Quiet pull-through shaded by white pines, with water and 50-amp service.',68,8,true),('Heron Point 14','Tent Site','Walk-in tent site near the water with a private fire ring and picnic table.',42,4,false),('Cedar Grove 19','Tent Site','Roomy family tent site close to the bathhouse and playground.',48,6,false),('Sunset Cabin 03','Cabin Site','Cozy one-room cabin with a porch, fire ring and view across the lake.',119,4,true),('Loon Landing 22','Premium Site','Waterfront RV pad with full hookups, a covered table and room to spread out.',84,8,true)");
        if (await db.ExecuteScalarAsync<int>("SELECT count(*) FROM activities") == 0)
            await db.ExecuteAsync("INSERT INTO activities(title,description,starts_at,location) VALUES ('Lakeside campfire & s''mores','Settle in for stories, toasted marshmallows and a warm welcome from the camp hosts.',now() + interval '1 day 7 hours','North Shore fire circle'),('Morning paddle meetup','Bring your own kayak or borrow one of ours for an easy group paddle along the shore.',now() + interval '2 days 9 hours','Boat launch'),('Junior ranger scavenger hunt','A self-paced nature hunt with a small prize waiting at the camp store.',now() + interval '3 days 10 hours','Camp store porch'),('Friday fish fry','Fresh lake perch, live acoustic music and a relaxed end to the week.',now() + interval '5 days 6 hours','Lakeside Grill patio'),('Stargazing with the naturalist','Learn the big constellations and spot planets through a telescope.',now() + interval '6 days 8 hours','Meadow overlook')");
        if (await db.ExecuteScalarAsync<int>("SELECT count(*) FROM menu_items") == 0)
            await db.ExecuteAsync("INSERT INTO menu_items(category,name,description,price) VALUES ('Breakfast','Lakeside Sunrise Skillet','Eggs, breakfast potatoes, cheddar, peppers and toast.',13.50),('Breakfast','Blueberry Pancake Stack','Three buttermilk pancakes with local blueberries and maple syrup.',11.00),('Lunch','Dockside Burger','Griddled beef, cheddar, crispy onions, pickles and fries.',15.00),('Lunch','Cedar Chicken Wrap','Grilled chicken, greens, tomato, ranch and a warm tortilla.',13.50),('Dinner','Friday Lake Perch Basket','Crisp lake perch, slaw, fries and tartar sauce.',18.00),('Dinner','Campfire Flatbread','Mozzarella, roasted vegetables, pesto and a bright side salad.',16.00),('Kids','Little Loon Grilled Cheese','Cheddar toastie with apple slices and a cookie.',7.50),('Drinks','Wild Berry Lemonade','House lemonade, wild berry syrup and fresh mint.',4.75),('Drinks','Campground Cocoa','Rich hot cocoa topped with toasted marshmallows.',4.25)");
        if (await db.ExecuteScalarAsync<int>("SELECT count(*) FROM app_users") == 0)
        {
            var username = configuration["Admin:Username"] ?? "admin"; var rawPassword = configuration["Admin:Password"] ?? throw new InvalidOperationException("Set Admin:Password.");
            await db.ExecuteAsync("INSERT INTO app_users(username,password_hash) VALUES (@username,@hash)", new { username, hash = password.Hash(rawPassword) });
        }
    }
}
