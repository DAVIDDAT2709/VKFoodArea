package crc64bb2bca98788d8d34;


public class NarrationService_AndroidTtsInitListener
	extends java.lang.Object
	implements
		mono.android.IGCUserPeer,
		android.speech.tts.TextToSpeech.OnInitListener
{
/** @hide */
	public static final String __md_methods;
	static {
		__md_methods = 
			"n_onInit:(I)V:GetOnInit_IHandler:Android.Speech.Tts.TextToSpeech+IOnInitListenerInvoker, Mono.Android, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null\n" +
			"";
		mono.android.Runtime.register ("VKFoodArea.Services.NarrationService+AndroidTtsInitListener, VKFoodArea", NarrationService_AndroidTtsInitListener.class, __md_methods);
	}

	public NarrationService_AndroidTtsInitListener ()
	{
		super ();
		if (getClass () == NarrationService_AndroidTtsInitListener.class) {
			mono.android.TypeManager.Activate ("VKFoodArea.Services.NarrationService+AndroidTtsInitListener, VKFoodArea", "", this, new java.lang.Object[] {  });
		}
	}

	public void onInit (int p0)
	{
		n_onInit (p0);
	}

	private native void n_onInit (int p0);

	private java.util.ArrayList refList;
	public void monodroidAddReference (java.lang.Object obj)
	{
		if (refList == null)
			refList = new java.util.ArrayList ();
		refList.add (obj);
	}

	public void monodroidClearReferences ()
	{
		if (refList != null)
			refList.clear ();
	}
}
