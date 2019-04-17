using System;

using Android.App;
using Android.Content.PM;
using Android.Runtime;
using Android.Views;
using Android.Widget;
using Android.OS;
using Android.Gms.Maps;
using Android.Support.V7.App;
using Android.Gms.Location;
using Android.Locations;
using Android.Gms.Common.Apis;
using Android.Gms.Common;
using Android.Gms.Maps.Model;
using System.Threading.Tasks;
using Android.Gms.Location.Places;
using Android.Graphics;
using Java.Util;
using System.Collections.Generic;
using Android.Content;
using Android.Util;
using Plugin.Geofencing;

namespace LocationTest.Droid
{
    [Activity(Label = "LocationTest", Icon = "@mipmap/icon", Theme = "@style/MainTheme", MainLauncher = true,
        ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation)]
    public class MainActivity : AppCompatActivity,
        IOnMapReadyCallback,
        GoogleApiClient.IConnectionCallbacks,
        Android.Locations.ILocationListener,
        Android.Gms.Location.ILocationListener
    {
        private GoogleMap mMap;
        private MapFragment mapFragment;
        private GoogleApiClient apiClient;

        readonly GeofenceTriggerReceiver receiver = new GeofenceTriggerReceiver();

        protected override void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);

            SetContentView(Resource.Layout.Main);
            SetUpMap();
            

            mapFragment = FragmentManager.FindFragmentById(Resource.Id.map) as MapFragment;
            mapFragment.GetMapAsync(this);

            if (GoogleApiAvailability.Instance
                    .IsGooglePlayServicesAvailable(this) == 0)
            {
                apiClient = new GoogleApiClient.Builder(this)
                    .AddApi(LocationServices.API)
                    .AddConnectionCallbacks(this)
                    .Build();
            }

            EditText location = FindViewById<EditText>(Resource.Id.location);
            location.Text = "PUT A VALID ADDRESS IN HERE";

            Button addButton = FindViewById<Button>(Resource.Id.addButton);
            

            addButton.Click += (sender, e) =>
            {
                string text = location.Text;
                if (!string.IsNullOrEmpty(text))
                {
                    //TODO
                    SetupGeofence(text);
                    Log.Debug("SETUP GEOFENCE SHOULD HAVE EXECUTED" , "YEAH BOYYYYYY");
                }
            };

            

        }

        

        #region map setup stuff

        private void SetUpMap()
        {
            if (mMap == null)
            {
                FragmentManager.FindFragmentById<MapFragment>(Resource.Id.map).GetMapAsync(this);
            }
        }

        
        public void OnMapReady(GoogleMap googleMap)
        {
            // the map shows your current location
            mMap = googleMap;

            mMap.MapType = GoogleMap.MapTypeNormal;
            mMap.MyLocationEnabled = true;
            mMap.UiSettings.ZoomControlsEnabled = true;
            mMap.UiSettings.MyLocationButtonEnabled = true;

            if (apiClient != null)
            {
                if (!apiClient.IsConnected)
                    apiClient.Connect();
            }
        }


        #endregion

        Color missColor = Color.Argb(0x30, 0xff, 0, 0);
        Color hitColor = Color.Argb(0x30, 0, 0xff, 0);

        private async void SetupGeofence(string location)
        {
            // sets up the geofence using the coordinates or the location search
            //make sure when testing you put a valid address so be mindful of spelling
            // also try to use an address close to your current location
            LatLng coord = await GetCoordinate(location);
            if (coord != null)
            {
                CircleOptions circleOptions = new CircleOptions()
                    .InvokeCenter(coord)
                    .InvokeFillColor(missColor)
                    .InvokeRadius(50);
                var circle = mMap.AddCircle(circleOptions);

                //TODO addproximityalert method
                AddProximityAlert(
                    coord.Latitude, coord.Longitude, location,
                    _ => circle.FillColor = hitColor,
                    requestId => 
                    {
                        circle.FillColor = missColor;
                        RemoveProximityAlert(requestId, circle);
                    });
            }
        }

        private async Task<LatLng> GetCoordinate(string place)
        {
            if (Geocoder.IsPresent)
            {
                Geocoder coder = new Geocoder(this);
                var results = await coder.GetFromLocationNameAsync(place, 1);
                if (results.Count >= 1)
                {
                    return new LatLng(
                        results[0].Latitude,
                        results[0].Longitude);
                }

            }

            return null;
        }

        private int requestCode = 1;

        readonly Dictionary<int, PendingIntent> activeGeofences = new Dictionary<int, PendingIntent>();


        private void AddProximityAlert(double latitude, double longitude,
            string poiName, Action<int> enterWork = null,
            Action<int> exitWork = null)
        {
            #region SETUP CODE FOR RECEIVER

            //NOTE: this hash region does not actually build the geofence it is just setup code.
            //register enter and exit actions
            //this is the method that talks to the GeofenceTriggerReceiver
            
            receiver.RegisterActions(requestCode, enterWork, exitWork);

            //creating the receiver
            Bundle extras = new Bundle();
            extras.PutString("name", poiName);
            extras.PutInt("id", requestCode);
            Intent intent = new Intent(GeofenceTriggerReceiver.IntentName);
            intent.PutExtra(GeofenceTriggerReceiver.IntentName, extras);
            var pendingIntent = PendingIntent.GetBroadcast(this, requestCode, intent, PendingIntentFlags.CancelCurrent);

            #endregion

            #region building the geofence
            if (apiClient != null)
            {
                IGeofence geofence = new GeofenceBuilder()
                    .SetTransitionTypes(Geofence.GeofenceTransitionEnter | Geofence.GeofenceTransitionExit)
                    .SetCircularRegion(latitude, longitude, 50)
                    .SetRequestId(requestCode.ToString())
                    .SetExpirationDuration(Geofence.NeverExpire)
                    .Build();

                GeofencingRequest request = new GeofencingRequest.Builder()
                    .AddGeofence(geofence)
                    .Build();

                LocationServices.GeofencingApi.AddGeofences(apiClient, request, pendingIntent);
            }

            activeGeofences.Add(requestCode, pendingIntent);

            //requestcode increases as geofence locations increase
            requestCode++;


            #endregion


        }

        void OnRemoveButtonClickedAsync(object sender, EventArgs e)
        {
            //TODO
        }

        async void RemoveProximityAlert(int requestId, Circle theCircle)
        {

            PendingIntent pendingIntent;
            if (activeGeofences.TryGetValue(requestId, out pendingIntent))
            {

                if (apiClient != null)
                {
                   await LocationServices.GeofencingApi.RemoveGeofences(apiClient, pendingIntent);
                }
                else
                {
                    LocationManager locManager = LocationManager.FromContext(this);
                    locManager.RemoveProximityAlert(pendingIntent);
                }
            }

            activeGeofences.Remove(requestId);
            receiver.UnregisterAction(requestId);

            // Remove the circle after 1 second of exiting
            await Task.Delay(1000);
            theCircle.Remove();
            
            
        }

        Circle currentLocation;

        public void OnLocationChanged(Location location)
        {
            var pos = new LatLng(location.Latitude, location.Longitude);

            if (currentLocation == null)
            {
                var options = new CircleOptions()
                    .InvokeCenter(pos)
                    .InvokeRadius(12)
                    .InvokeFillColor(Color.CornflowerBlue)
                    .InvokeStrokeColor(Color.White)
                    .InvokeStrokeWidth(4);
                currentLocation = mMap.AddCircle(options);
            }
            else
            {
                currentLocation.Center = pos;
            }

            mMap.MoveCamera(CameraUpdateFactory.NewLatLng(pos));
        }

        protected override void OnStart()
        {
            base.OnStart();
            if (apiClient != null && mMap != null)
                apiClient.Connect();
        }

        protected override void OnStop()
        {
            base.OnStop();
            if (apiClient != null)
                apiClient.Disconnect();
        }

        public void OnConnected(Bundle connectionHint)
        {
            //TODO
        }

        public void OnConnectionSuspended(int cause)
        {
            
        }

        public void OnProviderDisabled(string provider)
        {
            
        }

        public void OnProviderEnabled(string provider)
        {
            
        }

        public void OnStatusChanged(string provider, [GeneratedEnum] Availability status, Bundle extras)
        {
            
        }

       
    }
}