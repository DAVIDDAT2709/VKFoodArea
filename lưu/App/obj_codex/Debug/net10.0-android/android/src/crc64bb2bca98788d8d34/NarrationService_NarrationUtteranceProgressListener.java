package crc64bb2bca98788d8d34;


public class NarrationService_NarrationUtteranceProgressListener
	extends android.speech.tts.UtteranceProgressListener
	implements
		mono.android.IGCUserPeer
{
/** @hide */
	public static final String __md_methods;
	static {
		__md_methods = 
			"n_onStart:(Ljava/lang/String;)V:GetOnStart_Ljava_lang_String_Handler\n" +
			"n_onDone:(Ljava/lang/String;)V:GetOnDone_Ljava_lang_String_Handler\n" +
			"n_onError:(Ljava/lang/String;)V:GetOnError_Ljava_lang_String_Handler\n" +
			"n_onStop:(Ljava/lang/String;Z)V:GetOnStop_Ljava_lang_String_ZHandler\n" +
			"";
		mono.android.Runtime.register ("VKFoodArea.Services.NarrationService+NarrationUtteranceProgressListener, VKFoodArea", NarrationService_NarrationUtteranceProgressListener.class, __md_methods);
	}

	public NarrationService_NarrationUtteranceProgressListener ()
	{
		super ();
		if (getClass () == NarrationService_NarrationUtteranceProgressListener.class) {
			mono.android.TypeManager.Activate ("VKFoodArea.Services.NarrationService+NarrationUtteranceProgressListener, VKFoodArea", "", this, new java.lang.Object[] {  });
		}
	}

	public void onStart (java.lang.String p0)
	{
		n_onStart (p0);
	}

	private native void n_onStart (java.lang.String p0);

	public void onDone (java.lang.String p0)
	{
		n_onDone (p0);
	}

	private native void n_onDone (java.lang.String p0);

	public void onError (java.lang.String p0)
	{
		n_onError (p0);
	}

	private native void n_onError (java.lang.String p0);

	public void onStop (java.lang.String p0, boolean p1)
	{
		n_onStop (p0, p1);
	}

	private native void n_onStop (java.lang.String p0, boolean p1);

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
